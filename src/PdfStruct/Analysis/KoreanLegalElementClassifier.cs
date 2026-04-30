// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;
using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// Classifies text blocks using Korean legal-document structural markers
/// (편/장/절/관/항, plus the special standalone markers 전문 and 부칙).
/// Designed to be composed with a font-based classifier via
/// <see cref="CompositeElementClassifier"/>: this classifier produces a
/// <see cref="HeadingElement"/> when it recognizes a section marker and a
/// <see cref="ParagraphElement"/> otherwise, allowing a downstream
/// classifier to refine the paragraph fallback if it has stronger
/// evidence (e.g. font-size-based title detection).
/// </summary>
/// <remarks>
/// Article anchors (<c>제N조</c>) are deliberately <em>not</em> classified
/// as headings: the constitution alone has 130 articles, so promoting them
/// would flatten the document tree and defeat the table-of-contents
/// produced from heading levels. Article anchors stay as paragraphs and
/// are rendered as inline bold by the Markdown renderer.
/// </remarks>
public sealed partial class KoreanLegalElementClassifier : IElementClassifier
{
    /// <inheritdoc />
    public IReadOnlyList<ContentElement> Classify(
        IReadOnlyList<TextBlock> blocks, int pageNumber, ref int startId)
    {
        var results = new List<ContentElement>(blocks.Count);
        foreach (var block in blocks)
        {
            ContentElement element = TryClassifyHeading(block, pageNumber, ref startId)
                ?? (ContentElement)CreateParagraph(block, pageNumber, ref startId);
            results.Add(element);
        }
        return results;
    }

    /// <summary>Returns a <see cref="HeadingElement"/> when the block's first line matches a section marker, otherwise <c>null</c>.</summary>
    private static HeadingElement? TryClassifyHeading(TextBlock block, int pageNumber, ref int id)
    {
        var firstLine = FirstLine(block.Text);
        var match = SectionHeadingRegex().Match(firstLine);
        if (!match.Success) return null;

        var level = ResolveLevel(match);
        return new HeadingElement
        {
            Id = id++,
            PageNumber = pageNumber,
            BoundingBox = block.BoundingBox,
            HeadingLevel = level,
            Level = LevelLabel(level),
            Text = new TextProperties
            {
                Font = block.FontName,
                FontSize = block.FontSize,
                Content = block.Text.Trim()
            }
        };
    }

    private static ParagraphElement CreateParagraph(TextBlock block, int pageNumber, ref int id) => new()
    {
        Id = id++,
        PageNumber = pageNumber,
        BoundingBox = block.BoundingBox,
        Text = new TextProperties
        {
            Font = block.FontName,
            FontSize = block.FontSize,
            Content = block.Text.Trim()
        }
    };

    /// <summary>Extracts the first non-empty line of a block's text, trimmed.</summary>
    private static string FirstLine(string text)
    {
        var newline = text.IndexOf('\n');
        return (newline >= 0 ? text[..newline] : text).Trim();
    }

    /// <summary>Maps a regex match for a section marker to a heading level (편/장/전문/부칙=2, 절=3, 관/항=4).</summary>
    private static int ResolveLevel(Match match)
    {
        var unit = match.Groups["unit"].Value;
        if (string.IsNullOrEmpty(unit))
        {
            return 2; // 전문 / 부칙
        }
        return unit switch
        {
            "편" or "장" => 2,
            "절" => 3,
            "관" or "항" => 4,
            _ => 2
        };
    }

    private static string LevelLabel(int level) => level switch
    {
        1 => "Title",
        2 => "Section",
        3 => "Subsection",
        _ => $"Level {level}"
    };

    /// <summary>
    /// Matches Korean legal-document structural markers as the entire first
    /// line of a block. Captures the unit kanji into the named group
    /// <c>unit</c> for level resolution.
    /// </summary>
    [GeneratedRegex(
        @"^(제\s*\d+\s*(?<unit>편|장|절|관|항)(?=\s|$)|전문|부칙)(\s+.{0,40})?$",
        RegexOptions.Compiled)]
    private static partial Regex SectionHeadingRegex();
}
