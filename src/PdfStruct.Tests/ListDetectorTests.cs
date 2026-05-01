// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Analysis;
using PdfStruct.Models;
using Xunit;

namespace PdfStruct.Tests;

/// <summary>
/// Unit tests covering the Phase 1 list-detection behaviour specified in
/// <c>docs/list-detection-spec.md</c>. Tests target the spec sections in
/// order: label parsing (§ 4), run grouping (§ 5), the decimal sanity
/// filter (§ 6), and continuation absorption (§ 7).
/// </summary>
public class ListDetectorTests
{
    [Theory]
    [InlineData("1. The first item", "", 1, '.')]
    [InlineData("12) Second", "", 12, ')')]
    [InlineData("3: Third", "", 3, ':')]
    [InlineData("(1) Parenthesised", "(", 1, ')')]
    [InlineData("[1] Bracketed", "[", 1, ']')]
    [InlineData("(  12  ) Allows interior whitespace", "(", 12, ')')]
    public void TryParseLabel_RecognisesAllThreeShapes(string text, string expectedPrefix, int expectedNumber, char expectedTerminator)
    {
        var parsed = ListDetector.TryParseLabel(text);
        Assert.NotNull(parsed);
        var label = parsed!.Value.Label;
        Assert.Equal(expectedPrefix, label.Prefix);
        Assert.Equal(expectedNumber, label.Number);
        Assert.Equal(expectedTerminator, label.Terminator);
    }

    [Theory]
    [InlineData("1.5 Discussion")]
    [InlineData("3.14 Pi")]
    [InlineData("1.0 Zero")]
    public void TryParseLabel_RejectsDecimalsLikeSectionNumbers(string text)
    {
        Assert.Null(ListDetector.TryParseLabel(text));
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("")]
    [InlineData("1.")]
    [InlineData("1)")]
    [InlineData("(1)")]
    [InlineData(" 1. leading whitespace")]
    public void TryParseLabel_RejectsNonLabels(string text)
    {
        Assert.Null(ListDetector.TryParseLabel(text));
    }

    [Fact]
    public void Detect_GroupsSequentialArabicLabels()
    {
        var lines = new[]
        {
            Line("1. Apple", left: 50, baseline: 700),
            Line("2. Banana", left: 50, baseline: 685),
            Line("3. Cherry", left: 50, baseline: 670)
        };

        var result = ListDetector.Detect(lines);

        Assert.Single(result.Lists);
        var list = result.Lists[0];
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(1, list.Items[0].Number);
        Assert.Equal(2, list.Items[1].Number);
        Assert.Equal(3, list.Items[2].Number);
        Assert.Empty(result.ResidualLines);
    }

    [Fact]
    public void Detect_DropsSingletonLabel()
    {
        var lines = new[]
        {
            Line("1. Lonely", left: 50, baseline: 700),
            Line("Body paragraph that follows", left: 50, baseline: 685)
        };

        var result = ListDetector.Detect(lines);

        Assert.Empty(result.Lists);
        Assert.Equal(2, result.ResidualLines.Count);
    }

    [Fact]
    public void Detect_DoesNotMergeAcrossSkippedNumber()
    {
        var lines = new[]
        {
            Line("1. First", left: 50, baseline: 700),
            Line("2. Second", left: 50, baseline: 685),
            Line("4. Fourth — skip 3", left: 50, baseline: 670),
            Line("5. Fifth", left: 50, baseline: 655)
        };

        var result = ListDetector.Detect(lines);

        Assert.Equal(2, result.Lists.Count);
        Assert.Equal(2, result.Lists[0].Items.Count);
        Assert.Equal(2, result.Lists[1].Items.Count);
        Assert.Equal(1, result.Lists[0].Items[0].Number);
        Assert.Equal(4, result.Lists[1].Items[0].Number);
    }

    [Fact]
    public void Detect_DoesNotMergeAcrossDifferentTerminator()
    {
        var lines = new[]
        {
            Line("1. With period", left: 50, baseline: 700),
            Line("2) With paren", left: 50, baseline: 685),
            Line("3. Period again", left: 50, baseline: 670)
        };

        var result = ListDetector.Detect(lines);

        // "1." and "3." have matching terminator but skip "2.", so each is singleton.
        // "2)" is its own family with one item.
        Assert.Empty(result.Lists);
    }

    [Fact]
    public void Detect_DoesNotMergeAcrossDifferentPrefix()
    {
        var lines = new[]
        {
            Line("(1) Paren wrapped", left: 50, baseline: 700),
            Line("2. Different shape", left: 50, baseline: 685),
            Line("(2) Continues paren run", left: 50, baseline: 670)
        };

        var result = ListDetector.Detect(lines);

        Assert.Single(result.Lists);
        var list = result.Lists[0];
        Assert.Equal("(", list.CommonPrefix);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Detect_RejectsSectionNumberRuns()
    {
        // 1.5 / 1.6 / 1.7 / 1.8 — none should be parsed as labels because
        // they look like decimal numbers; the run never forms.
        var lines = new[]
        {
            Line("1.5 Discussion", left: 50, baseline: 700),
            Line("1.6 Conclusion", left: 50, baseline: 685),
            Line("1.7 Future work", left: 50, baseline: 670)
        };

        var result = ListDetector.Detect(lines);

        Assert.Empty(result.Lists);
        Assert.Equal(3, result.ResidualLines.Count);
    }

    [Fact]
    public void Detect_RespectsAlignmentLockOnceSameLeftEstablished()
    {
        // Two items at left=50, then a third item at left=200 (way past
        // tolerance). Once two items shared left=50 within tolerance the
        // candidate locks; the third item is rejected and starts a new run.
        var lines = new[]
        {
            Line("1. Aligned", left: 50, baseline: 700),
            Line("2. Aligned", left: 50, baseline: 685),
            Line("3. Drift right", left: 200, baseline: 670)
        };

        var result = ListDetector.Detect(lines);

        Assert.Single(result.Lists);
        Assert.Equal(2, result.Lists[0].Items.Count);
    }

    [Fact]
    public void Detect_AbsorbsContinuationLineAtSameOrGreaterIndent()
    {
        var lines = new[]
        {
            Line("1. Apple", left: 50, baseline: 700, fontSize: 10, height: 12),
            Line("    pie", left: 70, baseline: 688, fontSize: 10, height: 12),
            Line("2. Banana", left: 50, baseline: 670, fontSize: 10, height: 12)
        };

        var result = ListDetector.Detect(lines);

        Assert.Single(result.Lists);
        var first = result.Lists[0].Items[0];
        Assert.Contains("pie", first.Body);
        Assert.Equal(2, first.ClaimedLineIndices.Count);
    }

    [Fact]
    public void Detect_DoesNotAbsorbContinuationFurtherLeftThanItem()
    {
        var lines = new[]
        {
            Line("1. Apple", left: 50, baseline: 700, fontSize: 10, height: 12),
            Line("under-indented runaway", left: 20, baseline: 688, fontSize: 10, height: 12),
            Line("2. Banana", left: 50, baseline: 670, fontSize: 10, height: 12)
        };

        var result = ListDetector.Detect(lines);

        Assert.Single(result.Lists);
        Assert.Single(result.Lists[0].Items[0].ClaimedLineIndices);
    }

    [Fact]
    public void Detect_DoesNotAbsorbCandidateThatIsItselfALabel()
    {
        var lines = new[]
        {
            Line("1. Apple", left: 50, baseline: 700, fontSize: 10, height: 12),
            Line("(99) Different family", left: 50, baseline: 688, fontSize: 10, height: 12),
            Line("2. Banana", left: 50, baseline: 670, fontSize: 10, height: 12)
        };

        var result = ListDetector.Detect(lines);

        Assert.Single(result.Lists);
        Assert.Single(result.Lists[0].Items[0].ClaimedLineIndices);
    }

    [Fact]
    public void Detect_StripsLabelFromItemBody()
    {
        var lines = new[]
        {
            Line("1. The first item", left: 50, baseline: 700),
            Line("2. The second item", left: 50, baseline: 685)
        };

        var result = ListDetector.Detect(lines);

        Assert.Equal("The first item", result.Lists[0].Items[0].Body);
        Assert.Equal("The second item", result.Lists[0].Items[1].Body);
    }

    [Fact]
    public void Detect_PreservesUnclaimedLinesInResidualOrder()
    {
        var lines = new[]
        {
            Line("Heading text", left: 50, baseline: 720),
            Line("1. First", left: 50, baseline: 700),
            Line("2. Second", left: 50, baseline: 685),
            Line("Trailing paragraph", left: 50, baseline: 660)
        };

        var result = ListDetector.Detect(lines);

        Assert.Equal(2, result.ResidualLines.Count);
        Assert.Equal("Heading text", result.ResidualLines[0].Text);
        Assert.Equal("Trailing paragraph", result.ResidualLines[1].Text);
    }

    private static TextLineBlock Line(
        string text,
        double left,
        double baseline,
        double fontSize = 10.0,
        double height = 12.0)
    {
        var bbox = new BoundingBox(left, baseline - 1.0, left + 200.0, baseline - 1.0 + height);
        return new TextLineBlock(
            BoundingBox: bbox,
            Text: text,
            FontName: "TestFont",
            FontSize: fontSize,
            IsBold: false,
            BaselineY: baseline,
            AvgHeight: height);
    }
}
