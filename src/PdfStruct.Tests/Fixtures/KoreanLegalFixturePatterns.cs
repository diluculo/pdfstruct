// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;
using PdfStruct.Analysis;

namespace PdfStruct.Tests.Fixtures;

/// <summary>
/// Regex patterns and pre-built <see cref="HeadingPattern"/> sets for verifying
/// that Korean legal-document fixtures (e.g. the constitution) extract correctly.
/// These are validation helpers — NOT part of the library's classification logic.
/// </summary>
/// <remarks>
/// Tests inject these patterns into <see cref="RegexHeadingClassifier"/> via
/// <see cref="CompositeElementClassifier"/> to test the classifier composition
/// path without coupling the library to Korean-specific knowledge.
/// </remarks>
internal static class KoreanLegalFixturePatterns
{
    /// <summary>
    /// Matches chapter-, section-, sub-section-, and division-level structural
    /// markers as the entire first line of a block (편/장/절/관/항 plus the
    /// standalone markers 전문 and 부칙).
    /// </summary>
    public static readonly Regex SectionHeading = new(
        @"^(제\s*\d+\s*(편|장|절|관|항)(?=\s|$)|전문|부칙)(\s.*)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches an article-level marker (제N조) at the start of a line. Articles
    /// are content anchors, not headings — this regex is used for monotonicity
    /// assertions, not for promoting articles to heading rank.
    /// </summary>
    public static readonly Regex ArticleAnchor = new(
        @"^제\s*\d+\s*조(\s|$)",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns a chained set of <see cref="HeadingPattern"/>s mapping each
    /// Korean legal-document marker to its conventional heading depth:
    /// 편/장/전문/부칙 → H2, 절 → H3, 관/항 → H4.
    /// </summary>
    public static IEnumerable<HeadingPattern> AsHeadingPatterns()
    {
        yield return new HeadingPattern(
            new Regex(@"^(제\s*\d+\s*(편|장)(?=\s|$)|전문|부칙)(\s.*)?$", RegexOptions.Compiled),
            HeadingLevel: 2);
        yield return new HeadingPattern(
            new Regex(@"^제\s*\d+\s*절(?=\s|$).*$", RegexOptions.Compiled),
            HeadingLevel: 3);
        yield return new HeadingPattern(
            new Regex(@"^제\s*\d+\s*(관|항)(?=\s|$).*$", RegexOptions.Compiled),
            HeadingLevel: 4);
    }
}
