// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Analysis;
using PdfStruct.Rendering;
using PdfStruct.Safety;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfStruct;

/// <summary>
/// The result of a PDF conversion.
/// </summary>
/// <param name="Document">The parsed document model.</param>
/// <param name="Markdown">Markdown output, or <c>null</c> if not requested.</param>
/// <param name="Json">JSON output (OpenDataLoader-compatible), or <c>null</c> if not requested.</param>
public sealed record PdfStructResult(
    Models.PdfDocument Document,
    string? Markdown,
    string? Json);

/// <summary>
/// One row of heading-probability diagnostic output, produced by
/// <see cref="PdfStructParser.AnalyzeHeadingProbabilities"/>.
/// </summary>
/// <param name="PageNumber">1-indexed page number where the block appears.</param>
/// <param name="Block">The extracted text block, including font and layout signals.</param>
/// <param name="Breakdown">Per-signal contributions and total heading probability.</param>
/// <param name="ClassifiedAsHeading">Whether the total exceeds the configured threshold.</param>
public readonly record struct HeadingDiagnosticRow(
    int PageNumber,
    Analysis.TextBlock Block,
    Analysis.HeadingProbabilityBreakdown Breakdown,
    bool ClassifiedAsHeading);

/// <summary>
/// Main entry point for RAG-optimized PDF extraction.
/// Coordinates PdfPig → word grouping → XY-Cut++ reading order →
/// element classification → Markdown/JSON rendering.
/// </summary>
/// <example>
/// <code>
/// var parser = new PdfStructParser();
/// var result = parser.Parse("document.pdf");
/// Console.WriteLine(result.Markdown);
/// </code>
/// </example>
public sealed class PdfStructParser
{
    private readonly PdfStructOptions _options;
    private readonly ILayoutAnalyzer _layoutAnalyzer;
    private readonly IElementClassifier _classifier;

    /// <summary>Initializes with default options.</summary>
    public PdfStructParser() : this(new PdfStructOptions()) { }

    /// <summary>Initializes with the specified options.</summary>
    public PdfStructParser(PdfStructOptions options)
    {
        _options = options;
        _layoutAnalyzer = new XyCutLayoutAnalyzer(options.MinGapRatioX, options.MinGapRatioY);
        _classifier = new FontBasedElementClassifier(options.HeadingProbabilityThreshold);
    }

    /// <summary>Initializes with custom analyzer and classifier.</summary>
    public PdfStructParser(
        PdfStructOptions options, ILayoutAnalyzer layoutAnalyzer, IElementClassifier classifier)
    {
        _options = options;
        _layoutAnalyzer = layoutAnalyzer;
        _classifier = classifier;
    }

    /// <summary>Parses a PDF file by path.</summary>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public PdfStructResult Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath);
        return ParseInternal(pdf, Path.GetFileName(filePath));
    }

    /// <summary>Parses a PDF from a stream.</summary>
    public PdfStructResult Parse(Stream stream, string fileName = "document.pdf")
    {
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(stream);
        return ParseInternal(pdf, fileName);
    }

    /// <summary>Parses a PDF from a byte array.</summary>
    public PdfStructResult Parse(byte[] bytes, string fileName = "document.pdf")
    {
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        return ParseInternal(pdf, fileName);
    }

    /// <summary>
    /// Runs the parser pipeline up to (but not through) classification and
    /// returns the per-block heading-probability breakdown produced by the
    /// default <see cref="FontBasedElementClassifier"/>. Intended for
    /// threshold calibration and false-positive diagnosis — emit the rows
    /// to CSV and inspect score distributions across fixtures.
    /// </summary>
    /// <param name="filePath">Path to the input PDF.</param>
    /// <returns>One row per block, in extraction order, with score components and the threshold-based classification.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public IReadOnlyList<HeadingDiagnosticRow> AnalyzeHeadingProbabilities(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath);
        return AnalyzeHeadingProbabilitiesInternal(pdf);
    }

    /// <summary>Per-page extraction + scoring for diagnostic output.</summary>
    private IReadOnlyList<HeadingDiagnosticRow> AnalyzeHeadingProbabilitiesInternal(UglyToad.PdfPig.PdfDocument pdf)
    {
        var pageBlocks = new List<(int Page, IReadOnlyList<TextBlock> Blocks)>(pdf.NumberOfPages);
        var allBlocks = new List<TextBlock>();
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            var blocks = ExtractPageBlocks(pdf.GetPage(p));
            pageBlocks.Add((p, blocks));
            allBlocks.AddRange(blocks);
        }

        var classifier = new FontBasedElementClassifier(_options.HeadingProbabilityThreshold);
        classifier.Prepare(allBlocks);
        var stats = new DocumentStatistics(allBlocks);

        var rows = new List<HeadingDiagnosticRow>(allBlocks.Count);
        foreach (var (page, blocks) in pageBlocks)
        {
            foreach (var block in blocks)
            {
                var breakdown = classifier.ComputeHeadingProbabilityBreakdown(block, stats);
                rows.Add(new HeadingDiagnosticRow(
                    PageNumber: page,
                    Block: block,
                    Breakdown: breakdown,
                    ClassifiedAsHeading: breakdown.Total > classifier.HeadingProbabilityThreshold));
            }
        }
        return rows;
    }

    private PdfStructResult ParseInternal(UglyToad.PdfPig.PdfDocument pdf, string fileName)
    {
        var info = pdf.Information;
        var doc = new Models.PdfDocument
        {
            FileName = fileName,
            NumberOfPages = pdf.NumberOfPages,
            Author = info.Author,
            Title = info.Title,
            CreationDate = NormalizePdfDate(info.CreationDate),
            ModificationDate = NormalizePdfDate(info.ModifiedDate)
        };

        var pageBlocks = new Dictionary<int, IReadOnlyList<TextBlock>>(pdf.NumberOfPages);
        var pageHeights = new Dictionary<int, double>(pdf.NumberOfPages);
        var allBlocks = new List<TextBlock>();
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            var page = pdf.GetPage(p);
            pageHeights[p] = page.Height;
            var blocks = ExtractPageBlocks(page);
            pageBlocks[p] = blocks;
            allBlocks.AddRange(blocks);
        }

        _classifier.Prepare(allBlocks);

        var elementId = 1;
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            var elements = _classifier.Classify(pageBlocks[p], p, ref elementId);
            doc.Kids.AddRange(elements);
        }

        AssignHeadingLevels(doc.Kids);

        if (_options.ExcludeHeadersFooters)
        {
            var repeatingIds = RunningFurnitureDetector.DetectRepeatingIds(doc.Kids, pageHeights);
            if (repeatingIds.Count > 0)
                doc.Kids.RemoveAll(e => repeatingIds.Contains(e.Id));
        }

        RenumberInReadingOrder(doc.Kids);

        string? markdown = _options.Format.HasFlag(OutputFormat.Markdown)
            ? new MarkdownRenderer().Render(doc) : null;
        string? json = _options.Format.HasFlag(OutputFormat.Json)
            ? new JsonRenderer().Render(doc) : null;

        return new PdfStructResult(doc, markdown, json);
    }

    /// <summary>
    /// Assigns numeric heading levels 1..N to <see cref="Models.HeadingElement"/>
    /// instances by clustering them on typographic style (font size, font name,
    /// derived bold flag) and ordering style groups from largest/heaviest to
    /// smallest/lightest. Levels are capped at 6 (Markdown maximum).
    /// </summary>
    /// <remarks>
    /// Ports the OpenDataLoader-pdf <c>HeadingProcessor</c> level-assignment
    /// pass: a document's distinct heading styles form a hierarchy without
    /// the parser needing to reason about specific heading semantics. If
    /// every heading shares the same style, all become level 1, which is
    /// consistent if uninformative.
    /// </remarks>
    private static void AssignHeadingLevels(List<Models.ContentElement> kids)
    {
        var headings = kids.OfType<Models.HeadingElement>().ToList();
        if (headings.Count == 0) return;

        var styleGroups = headings
            .GroupBy(h => new TextStyleKey(h.Text.FontSize, IsBoldFontName(h.Text.Font), h.Text.Font))
            .OrderByDescending(g => g.Key.FontSize)
            .ThenByDescending(g => g.Key.IsBold)
            .ThenBy(g => g.Key.FontName, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < styleGroups.Count; i++)
        {
            var level = Math.Min(i + 1, 6);
            var label = HeadingLevelLabel(level);
            foreach (var heading in styleGroups[i])
            {
                heading.HeadingLevel = level;
                heading.Level = label;
            }
        }
    }

    /// <summary>Composite typographic-style key used for grouping headings.</summary>
    private readonly record struct TextStyleKey(double FontSize, bool IsBold, string FontName);

    /// <summary>
    /// Heuristic bold detection from a font name. Mirrors the
    /// <see cref="LineGroup.IsBold"/> derivation but is repeated here because
    /// only the rendered <see cref="Models.TextProperties.Font"/> string is
    /// preserved on the heading element.
    /// </summary>
    private static bool IsBoldFontName(string fontName) =>
        fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
        fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ||
        fontName.Contains("Black", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the structural label for a heading level, matching the convention used elsewhere in the library.</summary>
    private static string HeadingLevelLabel(int level) => level switch
    {
        1 => "Title",
        2 => "Section",
        3 => "Subsection",
        _ => $"Level {level}"
    };

    /// <summary>
    /// Stable-sorts elements top-to-bottom within each page and renumbers
    /// IDs sequentially. The per-page extraction pipeline emits blocks in
    /// reading order, but two blocks at very similar Y values can flip
    /// because XY-Cut chooses splits arbitrarily when gaps tie; sorting on
    /// Top descending corrects this without disturbing column structure
    /// (left/right column blocks have distinct Tops in practice, since
    /// each is a multi-line paragraph). Crucially, no Left-edge tiebreaker
    /// is applied — that was the cause of the multi-column interleaving
    /// observed before this commit.
    /// </summary>
    private static void RenumberInReadingOrder(List<Models.ContentElement> elements)
    {
        elements.Sort((a, b) =>
        {
            var pageCmp = a.PageNumber.CompareTo(b.PageNumber);
            if (pageCmp != 0) return pageCmp;
            return b.BoundingBox.Top.CompareTo(a.BoundingBox.Top);
        });

        for (var i = 0; i < elements.Count; i++)
            elements[i].Id = i + 1;
    }

    /// <summary>
    /// Converts a PDF date string in the form
    /// <c>D:YYYYMMDDHHmmSS[+|-]HH'mm'</c> (or its truncated variants) to an
    /// ISO 8601 representation like <c>2026-04-30T11:30:09+09:00</c>. Returns
    /// the input unchanged if it cannot be parsed.
    /// </summary>
    /// <remarks>
    /// PdfPig surfaces the PDF date dictionary as the raw string PDF stores
    /// it. Emitting that string directly into the OpenDataLoader-compatible
    /// JSON makes the field hostile to anyone reading it; ISO 8601 keeps
    /// the field usable while preserving the documented JSON shape.
    /// </remarks>
    private static string? NormalizePdfDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var s = raw.Trim();
        if (s.StartsWith("D:", StringComparison.Ordinal)) s = s[2..];

        var match = System.Text.RegularExpressions.Regex.Match(
            s,
            @"^(\d{4})(\d{2})?(\d{2})?(\d{2})?(\d{2})?(\d{2})?(?:([+\-Z])(\d{2})?(?:'(\d{2})'?)?)?$");
        if (!match.Success) return raw;

        try
        {
            var year = int.Parse(match.Groups[1].Value);
            var month = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
            var day = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 1;
            var hour = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
            var minute = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;
            var second = match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : 0;

            TimeSpan offset;
            if (!match.Groups[7].Success || match.Groups[7].Value == "Z")
            {
                offset = TimeSpan.Zero;
            }
            else
            {
                var sign = match.Groups[7].Value == "+" ? 1 : -1;
                var offsetHours = match.Groups[8].Success ? int.Parse(match.Groups[8].Value) : 0;
                var offsetMinutes = match.Groups[9].Success ? int.Parse(match.Groups[9].Value) : 0;
                offset = new TimeSpan(sign * offsetHours, sign * offsetMinutes, 0);
            }

            var dto = new DateTimeOffset(year, month, day, hour, minute, second, offset);
            return dto.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return raw;
        }
    }

    /// <summary>
    /// Extracts ordered text blocks for a single page: word grouping, hidden-text
    /// filtering, sanitation, reading-order analysis, and standalone-flag
    /// computation. Classification is performed later, after document-wide
    /// statistics are available.
    /// </summary>
    private IReadOnlyList<TextBlock> ExtractPageBlocks(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0) return [];

        var textBlocks = GroupWordsIntoBlocks(words);

        if (_options.FilterHiddenText)
            textBlocks = PromptInjectionFilter.Filter(textBlocks, page.Width, page.Height);

        textBlocks = TextSanitizer.ProcessBlocks(
            textBlocks,
            _options.SanitizeText,
            _options.InvalidCharacterReplacement,
            _options.SanitizationRules);

        var ordered = _layoutAnalyzer.DetermineReadingOrder(textBlocks);
        return WithStandaloneFlag(ordered);
    }

    /// <summary>
    /// Returns a copy of <paramref name="blocks"/> with each block's
    /// <see cref="TextBlock.IsStandalone"/> flag set based on whether any
    /// other block on the page overlaps its vertical row by more than 50%.
    /// </summary>
    private static IReadOnlyList<TextBlock> WithStandaloneFlag(IReadOnlyList<TextBlock> blocks)
    {
        var result = new List<TextBlock>(blocks.Count);
        for (var i = 0; i < blocks.Count; i++)
        {
            var standalone = true;
            for (var j = 0; j < blocks.Count; j++)
            {
                if (i == j) continue;
                if (VerticalOverlapRatio(blocks[i].BoundingBox, blocks[j].BoundingBox) > 0.5)
                {
                    standalone = false;
                    break;
                }
            }
            result.Add(blocks[i] with { IsStandalone = standalone });
        }
        return result;
    }

    /// <summary>
    /// Returns the fraction of <paramref name="a"/>'s vertical span that
    /// overlaps <paramref name="b"/>'s vertical span. Range <c>[0, 1]</c>.
    /// </summary>
    private static double VerticalOverlapRatio(Models.BoundingBox a, Models.BoundingBox b)
    {
        var top = Math.Min(a.Top, b.Top);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        var overlap = Math.Max(0, top - bottom);
        return a.Height > 0 ? overlap / a.Height : 0;
    }

    private static List<TextBlock> GroupWordsIntoBlocks(List<Word> words)
    {
        if (words.Count == 0) return [];

        // Sort top-to-bottom, then left-to-right. PdfPig's Word.BoundingBox.Bottom
        // is the higher visual Y for rotated glyphs and the lower visual Y for
        // regular glyphs, so sorting on it descending happens to keep
        // top-of-page words first in either orientation.
        var sorted = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();


        // Group words into lines by baseline proximity AND horizontal proximity.
        // The X-gap check prevents words at similar Y but in different columns
        // (e.g. an arxiv left-margin watermark glyph next to a body line, or
        // the left and right columns of a two-column paper) from being merged
        // into a single line just because they share a baseline.
        var lines = new List<LineGroup>();
        var current = new LineGroup(sorted[0]);

        for (int i = 1; i < sorted.Count; i++)
        {
            var w = sorted[i];
            var wLowerY = Math.Min(w.BoundingBox.Bottom, w.BoundingBox.Top);
            var yDist = Math.Abs(wLowerY - current.BaselineY);
            var xGap = w.BoundingBox.Left - current.Right;
            var maxXGap = Math.Max(15.0, current.AvgHeight * 1.5);

            if (yDist <= current.AvgHeight * 0.5 && xGap <= maxXGap)
            {
                current.Add(w);
            }
            else
            {
                lines.Add(current);
                current = new LineGroup(w);
            }
        }
        lines.Add(current);

        // Detect column right edges: lines that wrap inside a paragraph end
        // at the column's right edge, so the most common Right values across
        // the page are treated as column boundaries. Lines NOT ending at any
        // common right are short standalone elements (headings, captions).
        var rightBuckets = lines
            .GroupBy(l => Math.Round(l.Right / 5.0) * 5.0)
            .Where(g => g.Count() >= Math.Max(3, lines.Count / 8))
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToHashSet();

        bool LineEndsAtColumnRight(LineGroup line) =>
            rightBuckets.Contains(Math.Round(line.Right / 5.0) * 5.0);

        // Merge lines into blocks with column awareness. For each new line we
        // search the open blocks (one per active column) for the closest
        // previous line whose horizontal extent overlaps; ODL's
        // ParagraphProcessor calls this the "areOverlapping" check and uses
        // it as the column firewall without explicit column detection.
        var openBlocks = new List<List<LineGroup>>();
        foreach (var curr in lines)
        {
            var bestIdx = -1;
            var bestGap = double.MaxValue;

            for (var j = openBlocks.Count - 1; j >= 0; j--)
            {
                var lastLine = openBlocks[j][^1];
                var hasOverlap = lastLine.Right > curr.Left && curr.Right > lastLine.Left;
                if (!hasOverlap) continue;

                var gap = lastLine.Bottom - curr.Top;
                if (gap < 0) continue;

                var continues = IsLineContinuation(lastLine.Text)
                    && IsSameFontSize(lastLine, curr)
                    && LineEndsAtColumnRight(lastLine);
                var maxGap = lastLine.AvgHeight * (continues ? 3.0 : 1.5);
                if (gap > maxGap) continue;

                if (gap < bestGap)
                {
                    bestGap = gap;
                    bestIdx = j;
                }
            }

            if (bestIdx >= 0)
                openBlocks[bestIdx].Add(curr);
            else
                openBlocks.Add([curr]);
        }

        return openBlocks.Select(MergeLines).ToList();
    }

    private static TextBlock MergeLines(List<LineGroup> lines)
    {
        var text = string.Join("\n", lines.Select(l => l.Text));
        var bbox = lines.Select(l => l.Bbox).Aggregate((a, b) => a.Merge(b));
        var first = lines[0];
        return new TextBlock(
            bbox,
            text,
            first.FontName,
            first.AvgFontSize,
            first.IsBold,
            LineCount: lines.Count);
    }

    /// <summary>
    /// Returns <c>true</c> when a line ends mid-sentence — no Korean sentence
    /// terminator and no Latin period/colon/etc. at end-of-line. Such a line
    /// must merge with the next line even when the vertical gap is large
    /// (e.g. justified-paragraph trailing whitespace).
    /// </summary>
    /// <remarks>
    /// Closing quotes/brackets are stripped before checking the terminator
    /// so that <c>다."</c> still counts as terminated.
    /// </remarks>
    private static bool IsLineContinuation(string lineText)
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

    /// <summary>Returns <c>true</c> when two lines have effectively the same font size (within 10% or 1pt).</summary>
    private static bool IsSameFontSize(LineGroup a, LineGroup b)
    {
        var delta = Math.Abs(a.AvgFontSize - b.AvgFontSize);
        var tolerance = Math.Max(1.0, 0.1 * Math.Max(a.AvgFontSize, b.AvgFontSize));
        return delta <= tolerance;
    }

    private sealed class LineGroup
    {
        private readonly List<Word> _words = [];

        public LineGroup(Word w) => _words.Add(w);
        public void Add(Word w) => _words.Add(w);

        public double BaselineY => Math.Min(_words[0].BoundingBox.Bottom, _words[0].BoundingBox.Top);
        public double Left => _words.Min(w => w.BoundingBox.Left);
        public double Right => _words.Max(w => w.BoundingBox.Right);
        // PdfPig's bounding box can have Bottom > Top for rotated text (left-margin
        // arxiv watermarks, vertical sidebars). Normalise with min/max so downstream
        // gap and overlap math stays correct on mixed-orientation pages.
        public double Bottom => _words.Min(w => Math.Min(w.BoundingBox.Bottom, w.BoundingBox.Top));
        public double Top => _words.Max(w => Math.Max(w.BoundingBox.Bottom, w.BoundingBox.Top));
        public double Width => Right - Left;
        public double AvgHeight => _words.Average(w => Math.Abs(w.BoundingBox.Top - w.BoundingBox.Bottom));
        public double AvgFontSize => _words.Average(w => w.Letters.FirstOrDefault()?.PointSize ?? 12.0);
        public string FontName => _words[0].Letters.FirstOrDefault()?.FontName ?? "";
        public bool IsBold => FontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        public string Text => string.Join(" ", _words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));

        public Models.BoundingBox Bbox => new(Left, Bottom, Right, Top);
    }
}
