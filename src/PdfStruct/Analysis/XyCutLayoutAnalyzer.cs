// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// Defines a layout analyzer that determines reading order of text blocks on a PDF page.
/// </summary>
public interface ILayoutAnalyzer
{
    /// <summary>
    /// Sorts a collection of text blocks into correct reading order.
    /// </summary>
    IReadOnlyList<TextBlock> DetermineReadingOrder(IReadOnlyList<TextBlock> blocks);
}

/// <summary>
/// Represents a block of text with spatial coordinates, used as input to layout analysis.
/// </summary>
public sealed record TextBlock(
    BoundingBox BoundingBox,
    string Text,
    string FontName = "",
    double FontSize = 0,
    bool IsBold = false);

/// <summary>
/// Implements the XY-Cut++ algorithm for determining reading order on PDF pages.
/// Recursively partitions the page into logical blocks by finding the largest
/// horizontal/vertical gaps, then orders partitions in natural reading sequence.
/// </summary>
public sealed class XyCutLayoutAnalyzer : ILayoutAnalyzer
{
    private readonly double _minGapRatioX;
    private readonly double _minGapRatioY;

    /// <summary>
    /// Initializes a new instance of <see cref="XyCutLayoutAnalyzer"/>.
    /// </summary>
    /// <param name="minGapRatioX">Minimum horizontal gap ratio (vs page width) for X-cut. Default 0.01.</param>
    /// <param name="minGapRatioY">Minimum vertical gap ratio (vs page height) for Y-cut. Default 0.005.</param>
    public XyCutLayoutAnalyzer(double minGapRatioX = 0.01, double minGapRatioY = 0.005)
    {
        _minGapRatioX = minGapRatioX;
        _minGapRatioY = minGapRatioY;
    }

    /// <inheritdoc />
    public IReadOnlyList<TextBlock> DetermineReadingOrder(IReadOnlyList<TextBlock> blocks)
    {
        if (blocks.Count <= 1)
            return blocks;

        var pageBounds = ComputeBounds(blocks);
        var result = new List<TextBlock>(blocks.Count);
        RecursiveCut(blocks, pageBounds.Width, pageBounds.Height, result);
        return result;
    }

    private void RecursiveCut(
        IReadOnlyList<TextBlock> blocks,
        double pageWidth,
        double pageHeight,
        List<TextBlock> result)
    {
        if (blocks.Count <= 1)
        {
            result.AddRange(blocks);
            return;
        }

        var yCut = FindBestYCut(blocks, pageHeight);
        var xCut = FindBestXCut(blocks, pageWidth);

        if (yCut is null && xCut is null)
        {
            // No valid cut — fallback to top-to-bottom, left-to-right
            result.AddRange(blocks
                .OrderByDescending(b => b.BoundingBox.Top)
                .ThenBy(b => b.BoundingBox.Left));
            return;
        }

        if (yCut is not null && (xCut is null || yCut.Value.NormalizedGap >= xCut.Value.NormalizedGap))
        {
            // Horizontal cut: top first, then bottom
            var (top, bottom) = PartitionByY(blocks, yCut.Value.Position);
            RecursiveCut(top, pageWidth, pageHeight, result);
            RecursiveCut(bottom, pageWidth, pageHeight, result);
        }
        else
        {
            // Vertical cut: left first, then right
            var (left, right) = PartitionByX(blocks, xCut!.Value.Position);
            RecursiveCut(left, pageWidth, pageHeight, result);
            RecursiveCut(right, pageWidth, pageHeight, result);
        }
    }

    private CutResult? FindBestYCut(IReadOnlyList<TextBlock> blocks, double pageHeight)
    {
        var minGap = _minGapRatioY * pageHeight;
        var sorted = blocks.OrderByDescending(b => b.BoundingBox.Top).ToList();

        CutResult? best = null;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var gap = sorted[i].BoundingBox.Bottom - sorted[i + 1].BoundingBox.Top;
            if (gap < minGap) continue;

            var normalized = gap / pageHeight;
            var position = (sorted[i].BoundingBox.Bottom + sorted[i + 1].BoundingBox.Top) / 2.0;

            if (best is null || normalized > best.Value.NormalizedGap)
                best = new CutResult(position, normalized);
        }
        return best;
    }

    private CutResult? FindBestXCut(IReadOnlyList<TextBlock> blocks, double pageWidth)
    {
        var minGap = _minGapRatioX * pageWidth;
        var sorted = blocks.OrderBy(b => b.BoundingBox.Left).ToList();

        CutResult? best = null;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var gap = sorted[i + 1].BoundingBox.Left - sorted[i].BoundingBox.Right;
            if (gap < minGap) continue;

            var normalized = gap / pageWidth;
            var position = (sorted[i].BoundingBox.Right + sorted[i + 1].BoundingBox.Left) / 2.0;

            if (best is null || normalized > best.Value.NormalizedGap)
                best = new CutResult(position, normalized);
        }
        return best;
    }

    private static (List<TextBlock>, List<TextBlock>) PartitionByY(IReadOnlyList<TextBlock> blocks, double cutY)
    {
        var top = new List<TextBlock>();
        var bottom = new List<TextBlock>();
        foreach (var b in blocks)
            (b.BoundingBox.CenterY >= cutY ? top : bottom).Add(b);
        return (top, bottom);
    }

    private static (List<TextBlock>, List<TextBlock>) PartitionByX(IReadOnlyList<TextBlock> blocks, double cutX)
    {
        var left = new List<TextBlock>();
        var right = new List<TextBlock>();
        foreach (var b in blocks)
            (b.BoundingBox.CenterX <= cutX ? left : right).Add(b);
        return (left, right);
    }

    private static BoundingBox ComputeBounds(IReadOnlyList<TextBlock> blocks)
    {
        var left = double.MaxValue;
        var bottom = double.MaxValue;
        var right = double.MinValue;
        var top = double.MinValue;

        foreach (var b in blocks)
        {
            left = Math.Min(left, b.BoundingBox.Left);
            bottom = Math.Min(bottom, b.BoundingBox.Bottom);
            right = Math.Max(right, b.BoundingBox.Right);
            top = Math.Max(top, b.BoundingBox.Top);
        }
        return new BoundingBox(left, bottom, right, top);
    }

    private readonly record struct CutResult(double Position, double NormalizedGap);
}
