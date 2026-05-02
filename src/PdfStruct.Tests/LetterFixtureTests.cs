// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;
using Xunit;

namespace PdfStruct.Tests;

/// <summary>
/// Content regression tests for <c>letter.pdf</c>, the personal-letter
/// fixture whose display title <c>MY DEAREST</c> is set with non-zero
/// text-character spacing (<c>Tc</c>). The title is the canonical exercise
/// for the <see cref="Analysis.LetterGrouper"/> word extractor: PdfPig's
/// default extractor over-segments the letter-spaced glyphs into nine
/// single-letter words, while the in-tree grouper joins them on the
/// explicit space character that the underlying letter stream still carries
/// between <c>MY</c> and <c>DEAREST</c>.
/// </summary>
public sealed class LetterFixtureTests
{
    /// <summary>
    /// Asserts that the letter-spaced title is recovered as the contiguous
    /// string <c>MY DEAREST</c> on a single text-bearing element. The
    /// <see cref="Analysis.LetterGrouper"/> joins the per-glyph runs into
    /// the words <c>MY</c> and <c>DEAREST</c>; the relative-threshold line
    /// grouper in <c>PdfStructParser.GroupWordsIntoLines</c> then lands them
    /// on the same line because the title's only intra-line gap (≈48pt)
    /// stays below all three outlier thresholds.
    /// </summary>
    [Fact]
    public void LetterSpacedTitle_IsRecoveredAsContiguousMyDearest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "letter.pdf");
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var result = new PdfStructParser().Parse(path);
        var titleHolder = result.Document.Kids
            .Select(GetText)
            .FirstOrDefault(text => text is not null && text.Contains("MY DEAREST"));

        Assert.NotNull(titleHolder);
    }

    /// <summary>
    /// Returns the textual content of a text-bearing element, or <c>null</c>
    /// for non-text element types (lists, tables, images).
    /// </summary>
    private static string? GetText(ContentElement element) => element switch
    {
        ParagraphElement p => p.Text.Content,
        HeadingElement h => h.Text.Content,
        CaptionElement c => c.Text.Content,
        _ => null
    };
}
