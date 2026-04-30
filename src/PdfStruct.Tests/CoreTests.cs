// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Analysis;
using PdfStruct.Models;
using PdfStruct.Safety;
using Xunit;

namespace PdfStruct.Tests;

public class BoundingBoxTests
{
    [Fact]
    public void Merge_ShouldEncompassBothBoxes()
    {
        var a = new BoundingBox(10, 20, 100, 80);
        var b = new BoundingBox(50, 10, 150, 90);
        var merged = a.Merge(b);

        Assert.Equal(10, merged.Left);
        Assert.Equal(10, merged.Bottom);
        Assert.Equal(150, merged.Right);
        Assert.Equal(90, merged.Top);
    }

    [Fact]
    public void Overlaps_ShouldDetectOverlap()
    {
        var a = new BoundingBox(0, 0, 100, 100);
        var b = new BoundingBox(50, 50, 150, 150);
        Assert.True(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_ShouldDetectNonOverlap()
    {
        var a = new BoundingBox(0, 0, 50, 50);
        var b = new BoundingBox(100, 100, 200, 200);
        Assert.False(a.Overlaps(b));
    }

    [Fact]
    public void ToArray_ShouldMatchOdlFormat()
    {
        var bbox = new BoundingBox(72.0, 700.0, 540.0, 730.0);
        var arr = bbox.ToArray();
        Assert.Equal([72.0, 700.0, 540.0, 730.0], arr);
    }

    [Fact]
    public void FromArray_ShouldRoundTrip()
    {
        var original = new BoundingBox(10, 20, 30, 40);
        var restored = BoundingBox.FromArray(original.ToArray());
        Assert.Equal(original, restored);
    }
}

public class XyCutLayoutAnalyzerTests
{
    [Fact]
    public void SingleBlock_ShouldReturnAsIs()
    {
        var analyzer = new XyCutLayoutAnalyzer();
        var blocks = new[] { new TextBlock(new BoundingBox(0, 0, 100, 50), "Hello") };
        var result = analyzer.DetermineReadingOrder(blocks);
        Assert.Single(result);
        Assert.Equal("Hello", result[0].Text);
    }

    [Fact]
    public void TwoColumns_ShouldOrderLeftToRight()
    {
        var analyzer = new XyCutLayoutAnalyzer();
        var blocks = new[]
        {
            // Right column (added first to test reordering)
            new TextBlock(new BoundingBox(320, 500, 580, 520), "Right column text"),
            // Left column
            new TextBlock(new BoundingBox(30, 500, 280, 520), "Left column text"),
        };

        var result = analyzer.DetermineReadingOrder(blocks);
        Assert.Equal("Left column text", result[0].Text);
        Assert.Equal("Right column text", result[1].Text);
    }

    [Fact]
    public void StackedBlocks_ShouldOrderTopToBottom()
    {
        var analyzer = new XyCutLayoutAnalyzer();
        var blocks = new[]
        {
            // Bottom block (added first)
            new TextBlock(new BoundingBox(50, 100, 500, 150), "Bottom"),
            // Top block
            new TextBlock(new BoundingBox(50, 600, 500, 650), "Top"),
        };

        var result = analyzer.DetermineReadingOrder(blocks);
        Assert.Equal("Top", result[0].Text);
        Assert.Equal("Bottom", result[1].Text);
    }
}

public class TextSanitizerTests
{
    [Fact]
    public void ReplaceInvalidCharacters_ShouldReplaceReplacementAndNullCharacters()
    {
        var text = TextSanitizer.ReplaceInvalidCharacters("A\uFFFDB\0C", " ");

        Assert.Equal("A B C", text);
    }

    [Fact]
    public void ReplaceInvalidCharacters_ShouldPreserveWhenReplacementIsNull()
    {
        var input = "A\uFFFDB";
        var text = TextSanitizer.ReplaceInvalidCharacters(input, null);

        Assert.Equal(input, text);
    }

    [Fact]
    public void Sanitize_ShouldMaskCommonSensitiveValues()
    {
        var text = TextSanitizer.Process(
            "Email test@example.org from 192.168.1.10 using https://example.org/a",
            sanitizeText: true,
            invalidCharacterReplacement: " ",
            TextSanitizer.CreateDefaultRules());

        Assert.Equal(
            "Email email@example.com from 0.0.0.0 using https://example.com",
            text);
    }

    [Fact]
    public void Sanitize_ShouldPreferNonOverlappingLongerMatches()
    {
        var text = TextSanitizer.Process(
            "Card 4111-1111-1111-1111",
            sanitizeText: true,
            invalidCharacterReplacement: " ",
            TextSanitizer.CreateDefaultRules());

        Assert.Equal("Card 0000-0000-0000-0000", text);
    }
}
