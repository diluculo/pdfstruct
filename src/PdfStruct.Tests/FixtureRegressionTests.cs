// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;
using PdfStruct.Analysis;
using PdfStruct.Models;
using PdfStruct.Tests.Fixtures;
using Xunit;

namespace PdfStruct.Tests;

/// <summary>
/// Regression tests anchored to the PDF fixtures under <c>tests/fixtures/</c>.
/// They guard against silent regressions in reading order, paragraph
/// merging, and heading classification by leaning on the Korean
/// constitution's quantitatively verifiable structure.
/// </summary>
public class FixtureRegressionTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    /// <summary>
    /// Article numbers (제1조 ...) in the main constitution body must
    /// appear in monotonically non-decreasing order. The fixture contains
    /// a 부칙 (addendum) that re-uses 제1조 onward, so we anchor the
    /// assertion to the longest non-decreasing prefix and verify it covers
    /// the bulk of the constitution (≥ 100 articles).
    /// </summary>
    [Fact]
    public void KoreanConstitution_ArticleNumbersAreMonotonicInMainBody()
    {
        var path = FixturePath("kr_constitution.pdf");
        var parser = new PdfStructParser();
        var result = parser.Parse(path);

        var articleNumbers = result.Document.Kids
            .OfType<ParagraphElement>()
            .Select(p => KoreanLegalFixturePatterns.ArticleAnchor.Match(p.Text.Content.TrimStart()))
            .Where(m => m.Success)
            .Select(m => int.Parse(Regex.Match(m.Value, @"\d+").Value))
            .ToList();

        Assert.NotEmpty(articleNumbers);

        var prefix = new List<int> { articleNumbers[0] };
        for (var i = 1; i < articleNumbers.Count; i++)
        {
            if (articleNumbers[i] >= prefix[^1]) prefix.Add(articleNumbers[i]);
            else break;
        }

        Assert.True(prefix[^1] >= 100,
            $"Longest non-decreasing prefix peaks at {prefix[^1]}, expected ≥ 100. Sequence start: [{string.Join(", ", articleNumbers.Take(20))}…]");
    }

    /// <summary>
    /// No paragraph in the Korean constitution should consist solely of a
    /// short sentence terminator like <c>한다.</c> or <c>있다.</c> — those
    /// are mid-paragraph wrap-around tails that must merge with the
    /// preceding line by the line-continuation rule.
    /// </summary>
    [Fact]
    public void KoreanConstitution_NoOrphanedSentenceTails()
    {
        var path = FixturePath("kr_constitution.pdf");
        var parser = new PdfStructParser();
        var result = parser.Parse(path);

        var orphanPattern = new Regex(@"^[\w가-힣]{1,8}\.$", RegexOptions.Compiled);
        var orphans = result.Document.Kids
            .OfType<ParagraphElement>()
            .Where(p => orphanPattern.IsMatch(p.Text.Content.Trim()))
            .ToList();

        Assert.Empty(orphans);
    }

    /// <summary>
    /// When the Korean section-marker patterns are wired into a composite
    /// classifier ahead of the font-based default, every chapter (제N장),
    /// section (제N절), and sub-section (제N관) marker should be classified
    /// as a heading — verifying that the
    /// <see cref="RegexHeadingClassifier"/> + <see cref="CompositeElementClassifier"/>
    /// composition path works end-to-end.
    /// </summary>
    [Fact]
    public void KoreanConstitution_SectionMarkersBecomeHeadingsWhenPatternsInjected()
    {
        var path = FixturePath("kr_constitution.pdf");
        var options = new PdfStructOptions();
        var classifier = new CompositeElementClassifier(
            new RegexHeadingClassifier(KoreanLegalFixturePatterns.AsHeadingPatterns()),
            new FontBasedElementClassifier(options.HeadingProbabilityThreshold));
        var parser = new PdfStructParser(
            options,
            new XyCutLayoutAnalyzer(options.MinGapRatioX, options.MinGapRatioY),
            classifier);

        var result = parser.Parse(path);

        var headingTexts = result.Document.Kids
            .OfType<HeadingElement>()
            .Select(h => h.Text.Content.Split('\n')[0].Trim())
            .ToList();

        // The fixture has chapters 제1장 through 제10장 — at least 10 of
        // them should appear as headings after pattern injection.
        var chapterHeadings = headingTexts
            .Count(t => Regex.IsMatch(t, @"^제\s*\d+\s*장(\s|$)"));
        Assert.True(chapterHeadings >= 10,
            $"Expected ≥ 10 chapter headings (제N장), got {chapterHeadings}. Headings: [{string.Join(", ", headingTexts)}]");
    }
}
