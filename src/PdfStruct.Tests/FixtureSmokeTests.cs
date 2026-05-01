// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;
using Xunit;

namespace PdfStruct.Tests;

/// <summary>
/// Infrastructure smoke tests that exercise extraction completion across
/// minimal fixtures. These guard against regressions in parse infrastructure,
/// font loading, encoding handling, and dependency compatibility — not
/// classification quality. For per-fixture content assertions see
/// <see cref="KoreanConstitutionFixtureTests"/> and
/// <see cref="UsConstitutionFixtureTests"/>.
/// </summary>
/// <remarks>
/// Smoke tests catch the failure modes that fire first when .NET, PdfPig,
/// or SkiaSharp version bumps land, when CI moves between Windows/Linux,
/// or when a new OS-specific code path triggers an unhandled exception
/// before any content assertion ever runs.
/// </remarks>
public sealed class FixtureSmokeTests
{
    /// <summary>
    /// The fixture set covered by smoke testing. Add a single line here to
    /// extend coverage. Excludes <c>kr_constitution.pdf</c> and
    /// <c>us_constitution.pdf</c> (covered by content tests) and the
    /// known-limitation fixtures whose extraction is intentionally
    /// quarantined in <see cref="KnownLimitationFixtureTests"/>.
    /// </summary>
    public static IEnumerable<object[]> SmokeFixtures =>
    [
        ["lorem_ipsum.pdf"],
        ["kr_lorem_ipsum.pdf"],
        ["letter.pdf"],
        ["minimal_document.pdf"],
        ["plos_utilizing_llm.pdf"],
        ["plos_game_based_education.pdf"],
    ];

    /// <summary>
    /// Verifies that extraction completes without exception and produces a
    /// non-empty document tree where every text-bearing element carries
    /// non-empty content. Does not assert classification correctness.
    /// </summary>
    [Theory]
    [MemberData(nameof(SmokeFixtures))]
    public void ExtractsWithoutError(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var parser = new PdfStructParser();
        var result = parser.Parse(path);

        Assert.NotNull(result);
        Assert.NotNull(result.Document);
        Assert.NotEmpty(result.Document.Kids);

        foreach (var element in result.Document.Kids)
        {
            var text = GetText(element);
            if (text is null) continue; // non-text-bearing element types (table/list/image)
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Text-bearing element on page {element.PageNumber} carries empty content (id={element.Id}).");
        }
    }

    [Fact]
    public void TableOfContents_ElementBoxesDoNotOverlap()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "table_of_contents.pdf");
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var parser = new PdfStructParser();
        var result = parser.Parse(path);
        var elements = result.Document.Kids.ToList();

        var overlaps = new List<string>();
        for (var i = 0; i < elements.Count; i++)
        {
            for (var j = i + 1; j < elements.Count; j++)
            {
                if (elements[i].PageNumber != elements[j].PageNumber)
                    continue;

                var area = elements[i].BoundingBox.IntersectionArea(elements[j].BoundingBox);
                if (area > 1.0)
                    overlaps.Add($"p{elements[i].PageNumber}:{elements[i].Id}<->{elements[j].Id}: {area:F1}");
            }
        }

        Assert.Empty(overlaps);
    }

    [Fact]
    public void TableOfContents_PageNumberColumnSurvivesRunningFurnitureFiltering()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "table_of_contents.pdf");
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var filtered = new PdfStructParser(new PdfStructOptions { ExcludeHeadersFooters = true }).Parse(path);
        var included = new PdfStructParser(new PdfStructOptions { ExcludeHeadersFooters = false }).Parse(path);

        var filteredPageNumberColumns = filtered.Document.Kids.Count(IsNumericPageColumn);
        var includedPageNumberColumns = included.Document.Kids.Count(IsNumericPageColumn);

        Assert.Equal(includedPageNumberColumns, filteredPageNumberColumns);
    }

    /// <summary>Returns the text content of a text-bearing element, or <c>null</c> for non-text element types.</summary>
    private static string? GetText(ContentElement element) => element switch
    {
        ParagraphElement p => p.Text.Content,
        HeadingElement h => h.Text.Content,
        CaptionElement c => c.Text.Content,
        _ => null
    };

    private static bool IsNumericPageColumn(ContentElement element)
    {
        var text = GetText(element);
        return !string.IsNullOrWhiteSpace(text)
            && text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c));
    }
}
