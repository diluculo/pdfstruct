// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// Per-signal breakdown of a block's heading probability. Useful for
/// calibration: emitting these to CSV reveals which signals discriminate
/// headings from body text on a given fixture and whether the threshold
/// produces clean separations.
/// </summary>
/// <param name="Base">Layout-only base probability (font ratio, standalone, single-line-short).</param>
/// <param name="FontSizeRarity">Font-size rarity rank, weighted by the classifier's size weight.</param>
/// <param name="FontWeightRarity">Font-weight rarity rank, weighted by the classifier's weight weight.</param>
/// <param name="Bulleted">Bonus added when the block begins with a list-label glyph.</param>
/// <param name="Total">Sum of the four contributing signals; classification compares this against the threshold.</param>
public readonly record struct HeadingProbabilityBreakdown(
    double Base,
    double FontSizeRarity,
    double FontWeightRarity,
    double Bulleted,
    double Total);

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
/// Classifies text blocks into headings and paragraphs by combining a base
/// heading probability (font ratio + standalone + length) with document-wide
/// rarity boosts on font size and font weight, plus a small bonus for blocks
/// that begin with a list-label glyph.
/// </summary>
/// <remarks>
/// Ports the OpenDataLoader-pdf <c>HeadingProcessor</c> approach. The probability
/// model is intentionally language-agnostic — every signal is layout- or
/// typography-derived, no text patterns are inspected. Documents whose
/// section headers carry no typographic distinction (e.g. legal corpora that
/// rely solely on textual markers like <c>제1장</c>) need a domain-specific
/// classifier composed in front of this one via
/// <see cref="CompositeElementClassifier"/>.
/// </remarks>
public sealed class FontBasedElementClassifier : IElementClassifier
{
    private readonly double _headingProbabilityThreshold;
    private readonly double _fontSizeRarityWeight;
    private readonly double _fontWeightRarityWeight;
    private readonly double _bulletedBoost;

    private DocumentStatistics? _statistics;

    /// <summary>
    /// Initializes a new instance of <see cref="FontBasedElementClassifier"/>.
    /// </summary>
    /// <param name="headingProbabilityThreshold">
    /// Probability above which a block is classified as a heading. ODL uses
    /// <c>0.75</c>; tune downward to recall more headings or upward to reduce
    /// false positives. Default <c>0.75</c>.
    /// </param>
    public FontBasedElementClassifier(double headingProbabilityThreshold = 0.75)
        : this(headingProbabilityThreshold, fontSizeRarityWeight: 0.5, fontWeightRarityWeight: 0.3, bulletedBoost: 0.1)
    {
    }

    /// <summary>
    /// Initializes a new instance with explicit weights for each signal.
    /// </summary>
    /// <param name="headingProbabilityThreshold">Threshold above which a block is classified as a heading.</param>
    /// <param name="fontSizeRarityWeight">Multiplier applied to the font-size rarity rank before summation.</param>
    /// <param name="fontWeightRarityWeight">Multiplier applied to the font-weight rarity rank before summation.</param>
    /// <param name="bulletedBoost">Constant boost added when the block begins with a recognised list-label glyph.</param>
    public FontBasedElementClassifier(
        double headingProbabilityThreshold,
        double fontSizeRarityWeight,
        double fontWeightRarityWeight,
        double bulletedBoost)
    {
        _headingProbabilityThreshold = headingProbabilityThreshold;
        _fontSizeRarityWeight = fontSizeRarityWeight;
        _fontWeightRarityWeight = fontWeightRarityWeight;
        _bulletedBoost = bulletedBoost;
    }

    /// <inheritdoc />
    public void Prepare(IReadOnlyList<TextBlock> documentBlocks)
    {
        _statistics = new DocumentStatistics(documentBlocks);
    }

    /// <inheritdoc />
    public IReadOnlyList<ContentElement> Classify(
        IReadOnlyList<TextBlock> blocks, int pageNumber, ref int startId)
    {
        var stats = _statistics ?? new DocumentStatistics(blocks);
        var elements = new List<ContentElement>(blocks.Count);

        foreach (var block in blocks)
        {
            var probability = ComputeHeadingProbability(block, stats);
            if (probability > _headingProbabilityThreshold)
                elements.Add(CreateHeading(block, pageNumber, ref startId));
            else
                elements.Add(CreateParagraph(block, pageNumber, ref startId));
        }
        return elements;
    }

    /// <summary>
    /// Computes a block's heading probability as the sum of the base
    /// probability and document-wide rarity boosts.
    /// </summary>
    public double ComputeHeadingProbability(TextBlock block, DocumentStatistics stats) =>
        ComputeHeadingProbabilityBreakdown(block, stats).Total;

    /// <summary>
    /// Computes the per-signal breakdown of a block's heading probability.
    /// Exposed for use by diagnostic tooling (calibration dumps): the same
    /// numbers consumed by classification, separated so they can be written
    /// to CSV and inspected against fixture expectations.
    /// </summary>
    /// <param name="block">The candidate block.</param>
    /// <param name="stats">Document statistics produced by <see cref="DocumentStatistics"/>.</param>
    /// <returns>The signal-by-signal breakdown plus the summed total.</returns>
    public HeadingProbabilityBreakdown ComputeHeadingProbabilityBreakdown(TextBlock block, DocumentStatistics stats)
    {
        var baseProbability = BaseHeadingProbability(block, stats);
        var sizeRarityBoost = stats.FontSizeRarity.GetBoost(stats.RoundFontSize(block.FontSize)) * _fontSizeRarityWeight;
        var weightRarityBoost = stats.FontWeightRarity.GetBoost(DocumentStatistics.WeightFor(block.IsBold)) * _fontWeightRarityWeight;
        var bulletedBoost = IsBulleted(block.Text) ? _bulletedBoost : 0.0;
        return new HeadingProbabilityBreakdown(
            Base: baseProbability,
            FontSizeRarity: sizeRarityBoost,
            FontWeightRarity: weightRarityBoost,
            Bulleted: bulletedBoost,
            Total: baseProbability + sizeRarityBoost + weightRarityBoost + bulletedBoost);
    }

    /// <summary>The probability threshold above which a block is classified as a heading.</summary>
    public double HeadingProbabilityThreshold => _headingProbabilityThreshold;

    /// <summary>
    /// Computes the base heading probability from layout-only signals: how
    /// much larger than the body baseline the font is, whether the block is
    /// alone on its row, and whether it is short enough to look like a
    /// title rather than a paragraph.
    /// </summary>
    /// <remarks>
    /// Stands in for the ODL <c>NodeUtils.headingProbability</c> primitive
    /// (which lives in a closed-source verapdf jar). Tuned conservatively —
    /// the rarity boosts contribute most of the discriminating signal in
    /// well-typeset corpora; this base merely shifts the floor.
    /// </remarks>
    private static double BaseHeadingProbability(TextBlock block, DocumentStatistics stats)
    {
        double probability = 0.0;

        if (stats.MedianFontSize > 0 && block.FontSize > stats.MedianFontSize)
        {
            var ratio = (block.FontSize - stats.MedianFontSize) / stats.MedianFontSize;
            probability += Math.Min(0.4, ratio * 0.6);
        }

        if (block.IsStandalone)
            probability += 0.15;

        if (block.LineCount == 1 && block.Text.Length < 100)
            probability += 0.1;

        return Math.Min(probability, 0.7);
    }

    /// <summary>
    /// Returns <c>true</c> when a block begins with a list-label glyph
    /// (ASCII bullets, geometric shapes, circled numerals). Matches a
    /// trimmed prefix of common bullet symbols; the full ODL set has ~1100
    /// Unicode entries that may be added later if needed.
    /// </summary>
    private static bool IsBulleted(string text)
    {
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.Length == 0) return false;
        return s_bulletGlyphs.IndexOf(trimmed[0]) >= 0;
    }

    /// <summary>
    /// A pragmatic subset of bullet/list-label glyphs covering ASCII,
    /// geometric, arrow, and circled-numeral families.
    /// </summary>
    private static readonly string s_bulletGlyphs =
        "-*+•·○●■□▪▫▶▷◆◇★☆→⇒"
      + "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳"
      + "❶❷❸❹❺❻❼❽❾❿";

    /// <summary>Creates a <see cref="HeadingElement"/> for the given block. Heading level is provisional (always 1) and is refined by a later pass that clusters headings by typographic style.</summary>
    private static HeadingElement CreateHeading(TextBlock block, int pageNumber, ref int id) => new()
    {
        Id = id++,
        PageNumber = pageNumber,
        BoundingBox = block.BoundingBox,
        HeadingLevel = 1,
        Level = "Doctitle",
        Text = ToTextProperties(block)
    };

    /// <summary>Creates a <see cref="ParagraphElement"/> for the given block.</summary>
    private static ParagraphElement CreateParagraph(TextBlock block, int pageNumber, ref int id) => new()
    {
        Id = id++,
        PageNumber = pageNumber,
        BoundingBox = block.BoundingBox,
        Text = ToTextProperties(block)
    };

    /// <summary>Builds the <see cref="TextProperties"/> payload from a block.</summary>
    private static TextProperties ToTextProperties(TextBlock block) => new()
    {
        Font = block.FontName,
        FontSize = block.FontSize,
        Content = block.Text.Trim()
    };
}
