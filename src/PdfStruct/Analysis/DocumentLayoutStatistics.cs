// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace PdfStruct.Analysis;

/// <summary>
/// Document-wide layout statistics derived from every
/// <see cref="DocumentTextBlock"/> in the document. Captures the
/// horizontal anchors that distinguish body content from headings,
/// page furniture, and other structural roles. Computed once before
/// classification, alongside the typographic <see cref="DocumentStatistics"/>.
/// </summary>
/// <remarks>
/// Frequency over absolute left positions identifies the body lane —
/// the most common anchor where ordinary running text sits. It is
/// <em>not</em> a hierarchy signal: in the Korean Constitution the body
/// lane (left ≈ 34) is occupied by article markers (제N조), which sit
/// structurally beneath chapters and sections. Use the body lane as a
/// baseline to recognise body text, and combine indent deviation with
/// marker patterns or typography to determine hierarchy.
/// </remarks>
public sealed class DocumentLayoutStatistics
{
    private const double LeftQuantisationStep = 0.5;

    /// <summary>
    /// The most-frequent quantised first-line left position across
    /// non-stats-only blocks. Returns <c>0</c> when the input is empty.
    /// </summary>
    public double BodyLaneLeft { get; }

    /// <summary>
    /// Initialises the statistics from every block in the document. Stats-only
    /// entries (synthesised list-stats blocks) are included in the population
    /// because they represent visible body text that upstream passes absorbed
    /// into list elements; excluding them would shift the body-lane mode.
    /// </summary>
    /// <param name="documentBlocks">The full document sequence.</param>
    public DocumentLayoutStatistics(IReadOnlyList<DocumentTextBlock> documentBlocks)
    {
        ArgumentNullException.ThrowIfNull(documentBlocks);

        var counts = new Dictionary<double, int>();
        foreach (var entry in documentBlocks)
        {
            var left = EffectiveFirstLineLeft(entry.Block);
            var key = Math.Round(left / LeftQuantisationStep) * LeftQuantisationStep;
            counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
        }

        BodyLaneLeft = counts.Count == 0
            ? 0.0
            : counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
    }

    /// <summary>
    /// Returns <c>true</c> when the block is visually centred on its page —
    /// both side margins are substantial (≥ 15% of page width) and
    /// approximately equal (within 5% of page width). The combined
    /// thresholds mean a body-width paragraph that happens to be centred
    /// inside its content lane (e.g. a justified paragraph inside narrow
    /// margins) does not qualify; only narrower titles and section
    /// headers placed centrally on the page do.
    /// </summary>
    /// <param name="block">The block to test.</param>
    /// <param name="pageWidth">Page width in PDF points. Returns <c>false</c> when not positive.</param>
    public static bool IsCenterAligned(TextBlock block, double pageWidth)
    {
        if (pageWidth <= 0) return false;

        var leftMargin = block.BoundingBox.Left;
        var rightMargin = pageWidth - block.BoundingBox.Right;
        if (leftMargin <= 0 || rightMargin <= 0) return false;

        var minMargin = pageWidth * 0.15;
        if (leftMargin < minMargin || rightMargin < minMargin) return false;

        var asymmetry = Math.Abs(leftMargin - rightMargin) / pageWidth;
        return asymmetry < 0.05;
    }

    /// <summary>
    /// Returns the block's first-line left in PDF points, falling back to
    /// the bounding box's minimum left when the line-level signal is not
    /// populated (legacy or test-fixture construction).
    /// </summary>
    private static double EffectiveFirstLineLeft(TextBlock block) =>
        double.IsNaN(block.FirstLineLeft) ? block.BoundingBox.Left : block.FirstLineLeft;
}
