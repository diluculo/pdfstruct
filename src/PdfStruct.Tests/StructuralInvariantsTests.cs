// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;
using Xunit;

namespace PdfStruct.Tests;

/// <summary>
/// Enforces the structural invariants stated in Phase 1 list-detection
/// spec § 8 (criteria 7 and 8 of § 13). For each committed fixture, the
/// extracted document must satisfy:
/// <list type="number">
///   <item>No two list elements at the page level have overlapping bounding boxes.</item>
///   <item>No paragraph element's bounding box is substantially contained inside any list element's bounding box.</item>
/// </list>
/// A failure here is a hard regression of detector output even when the
/// per-fixture markdown diff looks reasonable.
/// </summary>
public sealed class StructuralInvariantsTests
{
    private const double SubstantialContainmentRatio = 0.8;

    [Theory]
    [InlineData("kr_constitution.pdf")]
    [InlineData("us_constitution.pdf")]
    [InlineData("lorem_ipsum.pdf")]
    [InlineData("plos_utilizing_llm.pdf")]
    [InlineData("plos_game_based_education.pdf")]
    [InlineData("magazine_article.pdf")]
    [InlineData("table_of_contents.pdf")]
    [InlineData("letter.pdf")]
    [InlineData("minimal_document.pdf")]
    [InlineData("kr_lorem_ipsum.pdf")]
    public void NoSiblingListBoundingBoxesOverlap(string fixtureName)
    {
        var path = FixturePath(fixtureName);
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var parser = new PdfStructParser();
        var result = parser.Parse(path);

        var listsByPage = result.Document.Kids
            .OfType<ListElement>()
            .GroupBy(e => e.PageNumber)
            .ToList();

        foreach (var pageGroup in listsByPage)
        {
            var lists = pageGroup.ToList();
            for (var i = 0; i < lists.Count; i++)
            {
                for (var j = i + 1; j < lists.Count; j++)
                {
                    Assert.False(
                        lists[i].BoundingBox.Overlaps(lists[j].BoundingBox),
                        $"{fixtureName} page {pageGroup.Key}: list elements " +
                        $"#{lists[i].Id} and #{lists[j].Id} have overlapping bounding boxes.");
                }
            }
        }
    }

    [Theory]
    [InlineData("kr_constitution.pdf")]
    [InlineData("us_constitution.pdf")]
    [InlineData("lorem_ipsum.pdf")]
    [InlineData("plos_utilizing_llm.pdf")]
    [InlineData("plos_game_based_education.pdf")]
    [InlineData("magazine_article.pdf")]
    [InlineData("table_of_contents.pdf")]
    [InlineData("letter.pdf")]
    [InlineData("minimal_document.pdf")]
    [InlineData("kr_lorem_ipsum.pdf")]
    public void NoParagraphSubstantiallyInsideListBoundingBox(string fixtureName)
    {
        var path = FixturePath(fixtureName);
        Assert.True(File.Exists(path), $"Fixture missing on disk: {path}");

        var parser = new PdfStructParser();
        var result = parser.Parse(path);

        var elementsByPage = result.Document.Kids
            .GroupBy(e => e.PageNumber)
            .ToList();

        foreach (var pageGroup in elementsByPage)
        {
            var lists = pageGroup.OfType<ListElement>().ToList();
            var paragraphs = pageGroup.OfType<ParagraphElement>().ToList();

            foreach (var list in lists)
            {
                foreach (var paragraph in paragraphs)
                {
                    var ratio = ContainedAreaRatio(list.BoundingBox, paragraph.BoundingBox);
                    Assert.True(
                        ratio < SubstantialContainmentRatio,
                        $"{fixtureName} page {pageGroup.Key}: paragraph #{paragraph.Id} " +
                        $"is {ratio:P0} contained inside list #{list.Id}'s bounding box.");
                }
            }
        }
    }

    private static double ContainedAreaRatio(BoundingBox container, BoundingBox inner)
    {
        var overlapLeft = Math.Max(container.Left, inner.Left);
        var overlapRight = Math.Min(container.Right, inner.Right);
        var overlapBottom = Math.Max(container.Bottom, inner.Bottom);
        var overlapTop = Math.Min(container.Top, inner.Top);
        if (overlapRight <= overlapLeft || overlapTop <= overlapBottom) return 0.0;

        var overlapArea = (overlapRight - overlapLeft) * (overlapTop - overlapBottom);
        var innerArea = inner.Width * inner.Height;
        return innerArea > 0 ? overlapArea / innerArea : 0.0;
    }

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
