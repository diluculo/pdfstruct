// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// Phase 1 list detection. Implementation written from
// docs/list-detection-spec.md only; upstream Java sources were not
// consulted during coding (see docs/adr/0001-list-detection-license.md).

using System.Globalization;
using System.Text.RegularExpressions;
using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// A parsed Arabic-numeric label decomposed into its three textual parts.
/// </summary>
/// <param name="Prefix">Characters appearing before the digit run (empty, <c>(</c>, or <c>[</c>).</param>
/// <param name="Number">The integer recovered from the digit run.</param>
/// <param name="Terminator">Single non-digit character that terminates the label (e.g. <c>.</c>, <c>)</c>, <c>]</c>, <c>:</c>, <c>;</c>).</param>
internal readonly record struct ListLabel(string Prefix, int Number, char Terminator);

/// <summary>
/// One member of a detected list run.
/// </summary>
/// <param name="Number">The integer label number recovered from the start line.</param>
/// <param name="Body">Item body text — the start line's content with the label prefix stripped, joined to absorbed continuation lines with newlines.</param>
/// <param name="BoundingBox">Union of the start line and every absorbed continuation line.</param>
/// <param name="StartLineIndex">Index of the start line within the page's text-line sequence.</param>
/// <param name="ClaimedLineIndices">All page-line indices owned by this item (the start line plus any absorbed continuations).</param>
internal sealed record DetectedListItem(
    int Number,
    string Body,
    BoundingBox BoundingBox,
    int StartLineIndex,
    IReadOnlyList<int> ClaimedLineIndices);

/// <summary>
/// A confirmed run of two or more list items sharing a common label shape.
/// </summary>
/// <param name="CommonPrefix">Prefix string shared by every item's label.</param>
/// <param name="Terminator">The label-terminating glyph shared by every item.</param>
/// <param name="Items">Items in document order.</param>
/// <param name="BoundingBox">Union of every item's bounding box.</param>
/// <param name="FontSize">Font size of the first item's start line, propagated for downstream layout reasoning.</param>
/// <param name="FontName">Font name of the first item's start line.</param>
internal sealed record DetectedList(
    string CommonPrefix,
    char Terminator,
    IReadOnlyList<DetectedListItem> Items,
    BoundingBox BoundingBox,
    double FontSize,
    string FontName);

/// <summary>
/// Per-page output of <see cref="ListDetector.Detect"/>.
/// </summary>
/// <param name="Lists">Confirmed list runs in document order.</param>
/// <param name="ResidualLines">The page's text lines with claimed lines removed and document order preserved.</param>
/// <param name="ClaimedOriginalIndices">Original-index set of every line claimed by some list (for downstream cross-checking).</param>
internal sealed record ListDetectionResult(
    IReadOnlyList<DetectedList> Lists,
    IReadOnlyList<TextLineBlock> ResidualLines,
    IReadOnlySet<int> ClaimedOriginalIndices);

/// <summary>
/// Detects Arabic-numeric ordered lists in a page's text-line stream
/// before paragraph merging. See <c>docs/list-detection-spec.md</c> for the
/// authoritative behaviour specification; this class is the C# realisation
/// of that specification.
/// </summary>
internal static class ListDetector
{
    private const int MaxCandidateLookback = 500;
    private const double NearLeftToleranceMultiplier = 4.0;
    private const double InterLineSpacingMultiplier = 1.2;

    private static readonly Regex s_parenLabel = new(
        @"^\(\s*(\d+)\s*\)\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex s_bracketLabel = new(
        @"^\[\s*(\d+)\s*\]\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex s_trailingLabel = new(
        @"^(\d+)([.):;])\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex s_decimalLabel = new(
        @"^\d+\.\d+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Detects Arabic-numeric lists in a single page's text lines. The
    /// caller is responsible for sequencing this between the running
    /// furniture filter and the paragraph merger.
    /// </summary>
    /// <param name="pageLines">Text lines for one page in document order.</param>
    /// <returns>Confirmed lists, the residual line stream, and the set of claimed line indices.</returns>
    public static ListDetectionResult Detect(IReadOnlyList<TextLineBlock> pageLines)
    {
        if (pageLines.Count == 0)
            return EmptyResult(pageLines);

        var candidates = new List<Candidate>();
        for (var i = 0; i < pageLines.Count; i++)
        {
            var line = pageLines[i];
            if (string.IsNullOrWhiteSpace(line.Text) || line.FontSize < 1.0)
                continue;

            var parsed = TryParseLabel(line.Text);
            if (parsed is null)
                continue;

            var (label, labelLength) = parsed.Value;
            var matched = FindExtensibleCandidate(candidates, line, label);
            if (matched is not null)
                matched.Append(i, line, label, labelLength);
            else
                candidates.Add(new Candidate(i, line, label, labelLength));
        }

        var lists = new List<DetectedList>();
        var claimed = new HashSet<int>();
        foreach (var candidate in candidates)
        {
            if (candidate.ItemCount < 2) continue;
            if (candidate.AllLabelsLookLikeDecimals(s_decimalLabel)) continue;

            AbsorbContinuations(candidate, pageLines, claimed);
            foreach (var lineIndex in candidate.AllClaimedLineIndices()) claimed.Add(lineIndex);
            lists.Add(candidate.ToDetectedList());
        }

        lists.Sort((a, b) => a.Items[0].StartLineIndex.CompareTo(b.Items[0].StartLineIndex));

        var residual = new List<TextLineBlock>(pageLines.Count - claimed.Count);
        for (var i = 0; i < pageLines.Count; i++)
            if (!claimed.Contains(i))
                residual.Add(pageLines[i]);

        return new ListDetectionResult(lists, residual, claimed);
    }

    /// <summary>
    /// Attempts to parse the leading text of a line as one of the three
    /// recognised Arabic-numeric label shapes. Returns the parsed label and
    /// the character length of the matched label text (used to strip the
    /// label from the body), or <c>null</c> when no shape matches or when
    /// the leading digits look like a decimal value.
    /// </summary>
    public static (ListLabel Label, int MatchLength)? TryParseLabel(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var paren = s_parenLabel.Match(text);
        if (paren.Success)
        {
            var n = ParseInt(paren.Groups[1].Value);
            if (n is null) return null;
            return (new ListLabel("(", n.Value, ')'), paren.Length);
        }

        var bracket = s_bracketLabel.Match(text);
        if (bracket.Success)
        {
            var n = ParseInt(bracket.Groups[1].Value);
            if (n is null) return null;
            return (new ListLabel("[", n.Value, ']'), bracket.Length);
        }

        var trailing = s_trailingLabel.Match(text);
        if (trailing.Success)
        {
            var n = ParseInt(trailing.Groups[1].Value);
            if (n is null) return null;
            var terminator = trailing.Groups[2].Value[0];
            return (new ListLabel(string.Empty, n.Value, terminator), trailing.Length);
        }

        return null;
    }

    private static Candidate? FindExtensibleCandidate(
        List<Candidate> candidates,
        TextLineBlock line,
        ListLabel label)
    {
        var lookbackStart = Math.Max(0, candidates.Count - MaxCandidateLookback);
        for (var i = candidates.Count - 1; i >= lookbackStart; i--)
        {
            var candidate = candidates[i];
            if (candidate.CanExtend(line, label))
                return candidate;
        }
        return null;
    }

    private static void AbsorbContinuations(
        Candidate candidate,
        IReadOnlyList<TextLineBlock> pageLines,
        HashSet<int> alreadyClaimed)
    {
        for (var k = 0; k < candidate.ItemCount; k++)
        {
            var item = candidate.GetMutableItem(k);
            var nextStart = k + 1 < candidate.ItemCount
                ? candidate.GetMutableItem(k + 1).LineIndex
                : pageLines.Count;
            var startLine = pageLines[item.LineIndex];
            var previousBaseline = startLine.BaselineY;
            var typicalSpacing = Math.Max(startLine.AvgHeight, 1.0) * InterLineSpacingMultiplier;

            for (var j = item.LineIndex + 1; j < nextStart; j++)
            {
                if (alreadyClaimed.Contains(j)) break;

                var line = pageLines[j];
                var tol = SameLeftTolerance(line.FontSize);
                if (line.Left < startLine.Left - tol) break;
                if (Math.Abs(line.BaselineY - previousBaseline) > typicalSpacing) break;
                if (TryParseLabel(line.Text) is not null) break;

                item.Absorb(line, j);
                previousBaseline = line.BaselineY;
            }
        }
    }

    private static double SameLeftTolerance(double fontSize) => Math.Max(fontSize, 1.0) / 3.0;

    private static int? ParseInt(string s) =>
        int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static ListDetectionResult EmptyResult(IReadOnlyList<TextLineBlock> lines) =>
        new([], lines, new HashSet<int>());

    /// <summary>
    /// Mutable per-candidate accumulator. A candidate represents an
    /// in-progress run of label-bearing lines that have so far matched the
    /// label-shape constraints. It graduates to a confirmed list when it
    /// holds at least two items and survives the decimal sanity filter.
    /// </summary>
    private sealed class Candidate
    {
        private readonly List<MutableItem> _items = [];
        private bool _everyTransitionWasStrictSameLeft = true;

        public Candidate(int lineIndex, TextLineBlock line, ListLabel label, int labelLength)
        {
            _items.Add(new MutableItem(lineIndex, line, label, labelLength));
        }

        public int ItemCount => _items.Count;

        public MutableItem GetMutableItem(int index) => _items[index];

        public bool CanExtend(TextLineBlock line, ListLabel label)
        {
            var last = _items[^1];
            if (last.Label.Prefix != label.Prefix) return false;
            if (last.Label.Terminator != label.Terminator) return false;
            if (label.Number != last.Label.Number + 1) return false;

            var tol = SameLeftTolerance(last.Line.FontSize);
            var diff = Math.Abs(line.Left - last.Line.Left);
            var sameLeft = diff <= tol;
            var nearLeft = diff <= NearLeftToleranceMultiplier * tol;

            if (_items.Count >= 2 && _everyTransitionWasStrictSameLeft && !sameLeft)
                return false;

            return sameLeft || nearLeft;
        }

        public void Append(int lineIndex, TextLineBlock line, ListLabel label, int labelLength)
        {
            var last = _items[^1];
            var tol = SameLeftTolerance(last.Line.FontSize);
            var sameLeft = Math.Abs(line.Left - last.Line.Left) <= tol;
            if (!sameLeft) _everyTransitionWasStrictSameLeft = false;

            _items.Add(new MutableItem(lineIndex, line, label, labelLength));
        }

        public bool AllLabelsLookLikeDecimals(Regex decimalPattern)
        {
            foreach (var item in _items)
                if (!decimalPattern.IsMatch(item.RawLabelToken))
                    return false;
            return _items.Count > 0;
        }

        public IEnumerable<int> AllClaimedLineIndices()
        {
            foreach (var item in _items)
                foreach (var idx in item.ClaimedLineIndices)
                    yield return idx;
        }

        public DetectedList ToDetectedList()
        {
            var first = _items[0];
            var commonPrefix = first.Label.Prefix;
            var terminator = first.Label.Terminator;

            var detectedItems = new List<DetectedListItem>(_items.Count);
            BoundingBox listBox = first.BoundingBox;

            foreach (var item in _items)
            {
                detectedItems.Add(new DetectedListItem(
                    Number: item.Label.Number,
                    Body: item.BodyText,
                    BoundingBox: item.BoundingBox,
                    StartLineIndex: item.LineIndex,
                    ClaimedLineIndices: item.ClaimedLineIndices));
                listBox = listBox.Merge(item.BoundingBox);
            }

            return new DetectedList(
                commonPrefix,
                terminator,
                detectedItems,
                listBox,
                FontSize: first.Line.FontSize,
                FontName: first.Line.FontName);
        }
    }

    /// <summary>
    /// Internal mutable representation of one item while its body is being
    /// accumulated through continuation absorption.
    /// </summary>
    private sealed class MutableItem
    {
        private readonly List<int> _claimedLineIndices = [];
        private readonly List<string> _bodyLines = [];
        private BoundingBox _boundingBox;

        public MutableItem(int lineIndex, TextLineBlock line, ListLabel label, int labelLength)
        {
            LineIndex = lineIndex;
            Line = line;
            Label = label;
            _boundingBox = line.BoundingBox;
            _claimedLineIndices.Add(lineIndex);

            var clamped = Math.Min(labelLength, line.Text.Length);
            var stripped = line.Text.Length > clamped ? line.Text[clamped..] : string.Empty;
            _bodyLines.Add(stripped);

            RawLabelToken = string.Concat(
                label.Prefix,
                label.Number.ToString(CultureInfo.InvariantCulture),
                label.Terminator == '\0' ? string.Empty : label.Terminator.ToString());
        }

        public int LineIndex { get; }
        public TextLineBlock Line { get; }
        public ListLabel Label { get; }
        public string RawLabelToken { get; }
        public BoundingBox BoundingBox => _boundingBox;
        public IReadOnlyList<int> ClaimedLineIndices => _claimedLineIndices;
        public string BodyText => string.Join("\n", _bodyLines);

        public void Absorb(TextLineBlock continuation, int lineIndex)
        {
            _bodyLines.Add(continuation.Text);
            _boundingBox = _boundingBox.Merge(continuation.BoundingBox);
            _claimedLineIndices.Add(lineIndex);
        }
    }
}
