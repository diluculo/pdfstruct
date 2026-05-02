// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;
using Xunit;

namespace PdfStruct.Tests;

/// <summary>
/// Content regression tests for <c>lorem_ipsum.pdf</c>, the Latin-script
/// single-column body fixture. The third paragraph contains a justified
/// short line whose inter-word gaps (≈15pt) exceeded the previous
/// absolute 15pt word-to-line threshold by a hair, fragmenting the line
/// into seven single-word blocks (<c>lorem</c>, <c>nec</c>,
/// <c>gravida</c>, <c>placerat.</c>, <c>Phasellus</c>, <c>vel</c>,
/// <c>nibh</c>, <c>ipsum.</c>). The relative-threshold line grouper in
/// <c>PdfStructParser.GroupWordsIntoLines</c> keeps the line cohesive.
/// </summary>
public sealed class LoremIpsumFixtureTests
{
    /// <summary>
    /// Asserts that the justified body line's seven words land on a single
    /// text-bearing element. The line's median inter-word gap (≈15pt) sits
    /// well below the relative-outlier threshold, so all words remain
    /// grouped into one paragraph.
    /// </summary>
    [Fact]
    public void JustifiedBodyLineIsNotFragmentedIntoSingleWordBlocks()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "lorem_ipsum.pdf");
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var result = new PdfStructParser().Parse(path);
        var bodyContainer = result.Document.Kids
            .Select(GetText)
            .FirstOrDefault(text =>
                text is not null
                && text.Contains("gravida placerat.")
                && text.Contains("Phasellus vel nibh ipsum."));

        Assert.NotNull(bodyContainer);
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
