// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;
using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// Post-classification refinement pass that recovers heading-classification
/// consistency across blocks sharing a textual template. Real document
/// hierarchies often emit a series of structurally identical markers —
/// "Article. I.", "Article. II.", "Article. III." in a constitution, or
/// "Chapter 1", "Chapter 2" in a manual — where every member should hold
/// the same status as a heading. The per-block scoring inside
/// <see cref="FontBasedElementClassifier"/> can disagree on individual
/// members because their local context differs (a section heading sandwiched
/// between an unterminated paragraph and the body it introduces; a marker
/// that landed in the same row as another block and lost the standalone
/// boost). This pass groups blocks by a normalised template and, when at
/// least one member of a group is already a heading, promotes the rest.
/// </summary>
/// <remarks>
/// The approach is deliberately conservative: a template must contain
/// alphabetic content beyond its numeric placeholders before it qualifies,
/// which keeps pure-number patterns ("41", "100" in a TOC page-number
/// column) from being lifted as a class. Promotion never demotes — a
/// solitary P inside a mostly-H group rises, but a solitary H inside a
/// mostly-P group is left alone. The pass runs before
/// <see cref="PdfStructParser.AssignHeadingLevels"/> so newly-promoted
/// headings receive a level alongside their pre-existing siblings.
/// </remarks>
public static class TemplateClassConsistency
{
    private static readonly Regex s_romanNumeral = new(
        @"\b(?:M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3}))\b",
        RegexOptions.Compiled);

    private static readonly Regex s_arabicNumeral = new(
        @"\d+", RegexOptions.Compiled);

    private static readonly Regex s_whitespace = new(
        @"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex s_placeholderStripper = new(
        @"<[A-Z]+>", RegexOptions.Compiled);

    /// <summary>
    /// Promotes paragraph elements to headings when they share a normalised
    /// textual template with at least one already-classified heading. The
    /// promoted element inherits the heading level and label of the first
    /// matching heading in its template group, so a series like
    /// "Article. I." through "Article. VII." ends up at a single consistent
    /// level even when the underlying classifier disagreed on individual
    /// members.
    /// </summary>
    /// <param name="elements">The classified element list to refine in place.</param>
    public static void PromoteSharedTemplates(IList<ContentElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var byTemplate = new Dictionary<string, List<int>>();
        for (var i = 0; i < elements.Count; i++)
        {
            var content = ExtractContent(elements[i]);
            if (string.IsNullOrWhiteSpace(content)) continue;

            var template = ExtractTemplate(content);
            if (!HasAlphabeticContent(template)) continue;

            if (!byTemplate.TryGetValue(template, out var bucket))
            {
                bucket = new List<int>();
                byTemplate[template] = bucket;
            }
            bucket.Add(i);
        }

        foreach (var indices in byTemplate.Values)
        {
            if (indices.Count < 2) continue;

            HeadingElement? donor = null;
            foreach (var idx in indices)
            {
                if (elements[idx] is HeadingElement heading)
                {
                    donor = heading;
                    break;
                }
            }
            if (donor is null) continue;

            foreach (var idx in indices)
            {
                if (elements[idx] is not ParagraphElement paragraph) continue;

                elements[idx] = new HeadingElement
                {
                    Id = paragraph.Id,
                    PageNumber = paragraph.PageNumber,
                    BoundingBox = paragraph.BoundingBox,
                    HeadingLevel = donor.HeadingLevel,
                    Level = donor.Level,
                    Text = paragraph.Text
                };
            }
        }
    }

    /// <summary>Extracts a comparable normalised text from an element. Returns the empty string for elements without text content (lists, images, tables when added).</summary>
    private static string ExtractContent(ContentElement element) => element switch
    {
        HeadingElement h => h.Text.Content,
        ParagraphElement p => p.Text.Content,
        _ => string.Empty
    };

    /// <summary>
    /// Reduces a block of text to a structural template by collapsing
    /// whitespace, replacing arabic-numeral runs with <c>&lt;N&gt;</c>, and
    /// replacing roman-numeral words with <c>&lt;R&gt;</c>. The roman
    /// pattern matches the standard form (with subtractive notation) so
    /// "I", "II", "III", "IV", ..., "MCMXCIX" all collapse to the same
    /// placeholder; degenerate strings like a single English word "I"
    /// inside running prose collapse too, but the surrounding context
    /// keeps such templates unique.
    /// </summary>
    private static string ExtractTemplate(string text)
    {
        var normalised = s_whitespace.Replace(text.Trim(), " ");
        normalised = s_romanNumeral.Replace(normalised, "<R>");
        normalised = s_arabicNumeral.Replace(normalised, "<N>");
        return normalised;
    }

    /// <summary>
    /// Returns <c>true</c> when the template carries alphabetic content
    /// outside its placeholder tokens. A template that consists solely of
    /// digits and placeholders (e.g. a TOC page-number block reduced to
    /// <c>&lt;N&gt;</c>) is not a structural class — promoting such groups
    /// would lift unrelated single-number blocks together.
    /// </summary>
    private static bool HasAlphabeticContent(string template)
    {
        var stripped = s_placeholderStripper.Replace(template, string.Empty);
        for (var i = 0; i < stripped.Length; i++)
        {
            if (char.IsLetter(stripped[i])) return true;
        }
        return false;
    }
}
