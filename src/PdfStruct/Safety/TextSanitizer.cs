// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.RegularExpressions;
using PdfStruct.Analysis;

namespace PdfStruct.Safety;

/// <summary>
/// A regex-based text replacement rule used for optional content sanitization.
/// </summary>
/// <param name="Pattern">The pattern to match in extracted text.</param>
/// <param name="Replacement">The replacement text to emit.</param>
public sealed record TextSanitizationRule(Regex Pattern, string Replacement);

/// <summary>
/// Cleans extracted text by replacing invalid characters and optionally masking sensitive values.
/// </summary>
public static class TextSanitizer
{
    /// <summary>
    /// Creates the default sanitization rules for common sensitive values.
    /// </summary>
    public static List<TextSanitizationRule> CreateDefaultRules() =>
    [
        CreateRule(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", "email@example.com"),
        CreateRule(@"https?://[^\s<>""']+", "https://example.com"),
        CreateRule(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", "0.0.0.0"),
        CreateRule(@"\b(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}\b", "00:00:00:00:00:00"),
        CreateRule(@"\b(?:\d[ -]?){13,19}\b", "0000-0000-0000-0000"),
        CreateRule(@"\+?\d[\d\s().-]{7,}\d", "+00-0000-0000")
    ];

    /// <summary>
    /// Applies invalid-character replacement and optional sensitive value masking to text blocks.
    /// </summary>
    public static List<TextBlock> ProcessBlocks(
        IReadOnlyList<TextBlock> blocks,
        bool sanitizeText,
        string? invalidCharacterReplacement,
        IReadOnlyList<TextSanitizationRule> sanitizationRules)
    {
        var cleaned = new List<TextBlock>(blocks.Count);
        foreach (var block in blocks)
        {
            var text = Process(
                block.Text,
                sanitizeText,
                invalidCharacterReplacement,
                sanitizationRules);

            if (!string.IsNullOrWhiteSpace(text))
            {
                cleaned.Add(block with { Text = text });
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Applies invalid-character replacement and optional sensitive value masking to a string.
    /// </summary>
    public static string Process(
        string text,
        bool sanitizeText,
        string? invalidCharacterReplacement,
        IReadOnlyList<TextSanitizationRule> sanitizationRules)
    {
        var result = ReplaceInvalidCharacters(text, invalidCharacterReplacement);
        return sanitizeText ? Sanitize(result, sanitizationRules) : result;
    }

    /// <summary>
    /// Replaces common invalid extraction characters such as U+FFFD and NUL.
    /// </summary>
    public static string ReplaceInvalidCharacters(string text, string? replacement)
    {
        if (replacement is null || text.Length == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is '\uFFFD' or '\0')
            {
                builder.Append(replacement);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Masks sensitive values using non-overlapping regex replacements.
    /// </summary>
    public static string Sanitize(string text, IReadOnlyList<TextSanitizationRule> rules)
    {
        if (text.Length == 0 || rules.Count == 0)
        {
            return text;
        }

        var replacements = FindReplacements(text, rules);
        if (replacements.Count == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var position = 0;

        foreach (var replacement in replacements)
        {
            builder.Append(text, position, replacement.Start - position);
            builder.Append(replacement.Text);
            position = replacement.End;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    private static TextSanitizationRule CreateRule(string pattern, string replacement) =>
        new(new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant), replacement);

    private static List<Replacement> FindReplacements(
        string text,
        IReadOnlyList<TextSanitizationRule> rules)
    {
        var candidates = new List<Replacement>();
        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            var rule = rules[ruleIndex];
            foreach (Match match in rule.Pattern.Matches(text))
            {
                if (match.Success && match.Length > 0)
                {
                    candidates.Add(new Replacement(
                        match.Index,
                        match.Index + match.Length,
                        rule.Replacement,
                        ruleIndex));
                }
            }
        }

        candidates.Sort(static (left, right) =>
        {
            var startComparison = left.Start.CompareTo(right.Start);
            if (startComparison != 0)
            {
                return startComparison;
            }

            var lengthComparison = (right.End - right.Start).CompareTo(left.End - left.Start);
            return lengthComparison != 0
                ? lengthComparison
                : left.Priority.CompareTo(right.Priority);
        });

        var replacements = new List<Replacement>(candidates.Count);
        var currentEnd = 0;

        foreach (var candidate in candidates)
        {
            if (candidate.Start < currentEnd)
            {
                continue;
            }

            replacements.Add(candidate);
            currentEnd = candidate.End;
        }

        return replacements;
    }

    private readonly record struct Replacement(int Start, int End, string Text, int Priority);
}
