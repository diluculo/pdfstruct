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
    /// Asserts that the letter-spaced title's glyphs are joined into the
    /// contiguous words <c>MY</c> and <c>DEAREST</c> — the regression that
    /// motivated the in-tree word extractor. The two words may still land on
    /// separate elements because <c>MY</c> and <c>DEAREST</c> sit on
    /// independent baselines that the line-to-block merger does not yet
    /// join; that is tracked separately and is not what this test guards.
    /// </summary>
    [Fact]
    public void LetterSpacedTitle_GlyphsJoinIntoContiguousWords()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "letter.pdf");
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var result = new PdfStructParser().Parse(path);
        var allText = string.Join(
            "\n",
            result.Document.Kids
                .Select(GetText)
                .Where(text => !string.IsNullOrEmpty(text)));

        Assert.Contains("MY", allText);
        Assert.Contains("DEAREST", allText);

        Assert.DoesNotContain("M Y", allText);
        Assert.DoesNotContain("D E A R E S T", allText);
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
