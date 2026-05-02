// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace PdfStruct.Analysis;

/// <summary>
/// Sentence-boundary heuristics shared between the line-merge stage (which
/// uses them to decide whether a line of body text continues into the next)
/// and the heading classifier (which uses them to demote candidates that
/// are sandwiched mid-sentence between two body lines).
/// </summary>
internal static class SentenceFlow
{
    /// <summary>
    /// Returns <c>true</c> when a line ends mid-sentence — no Korean
    /// sentence terminator and no Latin period/colon/etc. at end-of-line.
    /// Such a line must merge with the next line even when the vertical gap
    /// is large (e.g. justified-paragraph trailing whitespace).
    /// </summary>
    /// <remarks>
    /// Closing quotes/brackets are stripped before checking the terminator
    /// so that <c>다."</c> still counts as terminated.
    /// </remarks>
    /// <param name="lineText">The text of the line to inspect.</param>
    public static bool IsLineContinuation(string lineText)
    {
        var trimmed = lineText.AsSpan().TrimEnd();
        while (trimmed.Length > 0 && IsClosingPunctuation(trimmed[^1]))
            trimmed = trimmed[..^1].TrimEnd();
        if (trimmed.Length == 0) return false;

        foreach (var terminator in s_koreanSentenceTerminators)
        {
            if (trimmed.EndsWith(terminator)) return false;
        }

        var last = trimmed[^1];
        if (last is '.' or '!' or '?' or ':' or ';') return false;

        return true;
    }

    /// <summary>
    /// Korean sentence-final endings (verb/adjective inflections plus
    /// rhetorical/exclamatory variants). A line ending with one of these is
    /// treated as a complete sentence, so the line-grouper does not pull the
    /// next line in as a continuation.
    /// </summary>
    private static readonly string[] s_koreanSentenceTerminators =
    [
        "다.", "요.", "오.", "음.", "함.", "임.", "라.", "자.",
        "니라.", "리라.", "로다.", "세.",
        "까?", "요?", "다!", "오!"
    ];

    /// <summary>Returns <c>true</c> for closing punctuation that may follow a sentence terminator.</summary>
    private static bool IsClosingPunctuation(char c) =>
        c is '"' or '\'' or '”' or '’' or ')' or ']' or '}' or '」' or '』' or '»';
}
