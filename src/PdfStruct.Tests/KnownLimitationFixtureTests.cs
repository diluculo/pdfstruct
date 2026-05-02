// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;
using Xunit;

namespace PdfStruct.Tests;

/// <summary>
/// Placeholder tests for fixtures whose extraction quality is known to be
/// poor in the current implementation. They are registered with
/// <see cref="FactAttribute.Skip"/> messages so xUnit reports them as
/// tracked-but-unaddressed — turning them into a low-friction TODO list
/// that surfaces every test run.
/// </summary>
/// <remarks>
/// When the underlying limitation is addressed, drop the <c>Skip</c>
/// argument and replace the test body with a real assertion.
/// </remarks>
public sealed class KnownLimitationFixtureTests
{
    /// <summary>
    /// Magazine-style display layouts used to produce false positives where
    /// pull quotes (large display type quoting body text) were classified
    /// as headings. The line-count decay and the sentence-flow demotion
    /// added to <c>FontBasedElementClassifier</c> together pull such blocks
    /// below the heading threshold; this test guards against regression.
    /// The fixture's only multi-line large-type block is the pull quote
    /// "The best wave is the one that…", which must not appear as a
    /// heading.
    /// </summary>
    [Fact]
    public void MagazineArticle_PullQuotesNotMisclassifiedAsHeadings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "magazine_article.pdf");
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var parser = new PdfStruct.PdfStructParser();
        var result = parser.Parse(path);

        var headingTexts = result.Document.Kids
            .OfType<HeadingElement>()
            .Select(h => h.Text.Content)
            .ToList();

        Assert.DoesNotContain(
            headingTexts,
            t => t.Contains("best wave", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tables-of-contents pages place page numbers at heading-sized type,
    /// causing the rarity-based classifier to score them as headings.
    /// Detection requires recognising the TOC layout pattern (entry text +
    /// trailing dot leader + page number) and excluding the page-number
    /// column from heading candidacy.
    /// </summary>
    [Fact(Skip = "TOC page-number false positives — see README 'What works, what doesn't'. Track resolution before unskipping.")]
    public void TableOfContents_PageNumbersNotMisclassifiedAsHeadings()
    {
        // Implementation pending TOC layout detection.
    }
}
