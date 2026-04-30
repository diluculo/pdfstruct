// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// Defines a classifier that determines the semantic type of text blocks.
/// </summary>
public interface IElementClassifier
{
    /// <summary>
    /// Optional pre-pass invoked once with every extracted block across the
    /// whole document, before any per-page <see cref="Classify"/> calls.
    /// Classifiers that need document-wide signals (font-size frequency,
    /// font-weight frequency, etc.) override this to compute and cache them.
    /// The default implementation is a no-op.
    /// </summary>
    /// <param name="documentBlocks">Every block extracted from the document, in arbitrary order.</param>
    void Prepare(IReadOnlyList<TextBlock> documentBlocks) { }

    /// <summary>
    /// Classifies text blocks into typed content elements.
    /// </summary>
    IReadOnlyList<ContentElement> Classify(
        IReadOnlyList<TextBlock> blocks, int pageNumber, ref int startId);
}

/// <summary>
/// Classifies text blocks into headings and paragraphs using font-size heuristics.
/// </summary>
public sealed class FontBasedElementClassifier : IElementClassifier
{
    private readonly double _headingSizeThreshold;

    /// <summary>
    /// Initializes a new instance of <see cref="FontBasedElementClassifier"/>.
    /// </summary>
    /// <param name="headingSizeThreshold">
    /// Ratio above median font size to classify as heading. Default 1.2 (20% larger).
    /// </param>
    public FontBasedElementClassifier(double headingSizeThreshold = 1.2)
        => _headingSizeThreshold = headingSizeThreshold;

    /// <inheritdoc />
    public IReadOnlyList<ContentElement> Classify(
        IReadOnlyList<TextBlock> blocks, int pageNumber, ref int startId)
    {
        if (blocks.Count == 0) return [];

        var fontSizes = blocks.Where(b => b.FontSize > 0).Select(b => b.FontSize).ToList();
        if (fontSizes.Count == 0)
        {
            var paragraphs = new List<ContentElement>(blocks.Count);
            foreach (var block in blocks)
            {
                paragraphs.Add(CreateParagraph(block, pageNumber, ref startId));
            }

            return paragraphs;
        }

        fontSizes.Sort();
        var median = fontSizes[fontSizes.Count / 2];
        var max = fontSizes[^1];
        var threshold = median * _headingSizeThreshold;

        var elements = new List<ContentElement>(blocks.Count);
        foreach (var block in blocks)
        {
            if (IsHeading(block, median, threshold))
            {
                var level = DetermineLevel(block.FontSize, median, max);
                elements.Add(new HeadingElement
                {
                    Id = startId++,
                    PageNumber = pageNumber,
                    BoundingBox = block.BoundingBox,
                    HeadingLevel = level,
                    Level = level switch { 1 => "Title", 2 => "Section", 3 => "Subsection", _ => $"Level {level}" },
                    Text = ToTextProperties(block)
                });
            }
            else
            {
                elements.Add(CreateParagraph(block, pageNumber, ref startId));
            }
        }
        return elements;
    }

    private static bool IsHeading(TextBlock block, double median, double threshold)
        => (block.FontSize >= threshold && block.Text.Trim().Length < 200)
        || (block.IsBold && block.FontSize >= median && block.Text.Trim().Length < 120);

    private static int DetermineLevel(double fontSize, double median, double max)
    {
        if (max <= median) return 1;
        var ratio = (fontSize - median) / (max - median);
        return ratio switch { >= 0.8 => 1, >= 0.6 => 2, >= 0.4 => 3, >= 0.2 => 4, >= 0.1 => 5, _ => 6 };
    }

    private static ParagraphElement CreateParagraph(TextBlock block, int pageNumber, ref int id)
        => new() { Id = id++, PageNumber = pageNumber, BoundingBox = block.BoundingBox, Text = ToTextProperties(block) };

    private static TextProperties ToTextProperties(TextBlock block)
        => new() { Font = block.FontName, FontSize = block.FontSize, Content = block.Text.Trim() };
}
