// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    /// Magazine-style display layouts produce false positives where pull
    /// quotes (large display type quoting body text) are misclassified as
    /// headings. Layout-level disambiguation — pull-quote shape, lateral
    /// position offset, surrounding text continuation — is required to
    /// distinguish them from real titles.
    /// </summary>
    [Fact(Skip = "Pull-quote false positives — see README 'What works, what doesn't'. Track resolution before unskipping.")]
    public void MagazineArticle_PullQuotesNotMisclassifiedAsHeadings()
    {
        // Implementation pending pull-quote disambiguation.
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
