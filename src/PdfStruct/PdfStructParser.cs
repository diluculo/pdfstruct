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
        _classifier = new FontBasedElementClassifier(options.HeadingSizeThreshold);
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

    private PdfStructResult ParseInternal(UglyToad.PdfPig.PdfDocument pdf, string fileName)
    {
        var info = pdf.Information;
        var doc = new Models.PdfDocument
        {
            FileName = fileName,
            NumberOfPages = pdf.NumberOfPages,
            Author = info.Author,
            Title = info.Title,
            CreationDate = info.CreationDate,
            ModificationDate = info.ModifiedDate
        };

        var elementId = 1;
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            var page = pdf.GetPage(p);
            var elements = ExtractPage(page, p, ref elementId);
            doc.Kids.AddRange(elements);
        }

        SortByReadingOrderAndRenumber(doc.Kids);

        string? markdown = _options.Format.HasFlag(OutputFormat.Markdown)
            ? new MarkdownRenderer().Render(doc) : null;
        string? json = _options.Format.HasFlag(OutputFormat.Json)
            ? new JsonRenderer().Render(doc) : null;

        return new PdfStructResult(doc, markdown, json);
    }

    /// <summary>
    /// Sorts elements into natural reading order (page top-to-bottom, then
    /// left-to-right) and renumbers their IDs sequentially. The XY-Cut
    /// analyzer emits elements cluster-by-cluster, so the parse-time IDs do
    /// not match the spatial order a reader expects; this pass corrects both
    /// the iteration order used by renderers and the IDs surfaced in JSON.
    /// </summary>
    /// <remarks>
    /// Y-coordinates are quantized into <see cref="ReadingOrderYTolerance"/>-point
    /// buckets so that sub-point measurement noise does not flip the order of
    /// elements that visually share a row.
    /// </remarks>
    private static void SortByReadingOrderAndRenumber(List<Models.ContentElement> elements)
    {
        elements.Sort(static (a, b) =>
        {
            var pageCmp = a.PageNumber.CompareTo(b.PageNumber);
            if (pageCmp != 0) return pageCmp;

            var aTop = QuantizeY(a.BoundingBox.Top);
            var bTop = QuantizeY(b.BoundingBox.Top);
            var topCmp = bTop.CompareTo(aTop);
            if (topCmp != 0) return topCmp;

            return a.BoundingBox.Left.CompareTo(b.BoundingBox.Left);
        });

        for (var i = 0; i < elements.Count; i++)
            elements[i].Id = i + 1;
    }

    private const double ReadingOrderYTolerance = 5.0;

    /// <summary>Buckets a Y coordinate into <see cref="ReadingOrderYTolerance"/>-point rows.</summary>
    private static double QuantizeY(double y) =>
        Math.Floor(y / ReadingOrderYTolerance) * ReadingOrderYTolerance;

    private IReadOnlyList<Models.ContentElement> ExtractPage(Page page, int pageNumber, ref int elementId)
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
        return _classifier.Classify(ordered, pageNumber, ref elementId);
    }

    private static List<TextBlock> GroupWordsIntoBlocks(List<Word> words)
    {
        if (words.Count == 0) return [];

        // Sort top-to-bottom, then left-to-right
        var sorted = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        // Group into lines by baseline proximity
        var lines = new List<LineGroup>();
        var current = new LineGroup(sorted[0]);

        for (int i = 1; i < sorted.Count; i++)
        {
            var w = sorted[i];
            var dist = Math.Abs(w.BoundingBox.Bottom - current.BaselineY);

            if (dist <= current.AvgHeight * 0.5)
                current.Add(w);
            else
            {
                lines.Add(current);
                current = new LineGroup(w);
            }
        }
        lines.Add(current);

        // Merge lines into blocks by spacing and horizontal overlap
        var blocks = new List<TextBlock>();
        var blockLines = new List<LineGroup> { lines[0] };

        for (int i = 1; i < lines.Count; i++)
        {
            var prev = lines[i - 1];
            var curr = lines[i];

            var overlap = Math.Min(prev.Right, curr.Right) - Math.Max(prev.Left, curr.Left);
            var overlapRatio = overlap / Math.Max(prev.Width, curr.Width);
            var gap = prev.Bottom - curr.Top;

            if (overlapRatio > 0.3 && gap >= 0 && gap < prev.AvgHeight * 1.5)
                blockLines.Add(curr);
            else
            {
                blocks.Add(MergeLines(blockLines));
                blockLines = [curr];
            }
        }
        if (blockLines.Count > 0) blocks.Add(MergeLines(blockLines));

        return blocks;
    }

    private static TextBlock MergeLines(List<LineGroup> lines)
    {
        var text = string.Join("\n", lines.Select(l => l.Text));
        var bbox = lines.Select(l => l.Bbox).Aggregate((a, b) => a.Merge(b));
        var first = lines[0];
        return new TextBlock(bbox, text, first.FontName, first.AvgFontSize, first.IsBold);
    }

    private sealed class LineGroup
    {
        private readonly List<Word> _words = [];

        public LineGroup(Word w) => _words.Add(w);
        public void Add(Word w) => _words.Add(w);

        public double BaselineY => _words[0].BoundingBox.Bottom;
        public double Left => _words.Min(w => w.BoundingBox.Left);
        public double Right => _words.Max(w => w.BoundingBox.Right);
        public double Bottom => _words.Min(w => w.BoundingBox.Bottom);
        public double Top => _words.Max(w => w.BoundingBox.Top);
        public double Width => Right - Left;
        public double AvgHeight => _words.Average(w => w.BoundingBox.Height);
        public double AvgFontSize => _words.Average(w => w.Letters.FirstOrDefault()?.PointSize ?? 12.0);
        public string FontName => _words[0].Letters.FirstOrDefault()?.FontName ?? "";
        public bool IsBold => FontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        public string Text => string.Join(" ", _words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));

        public Models.BoundingBox Bbox => new(Left, Bottom, Right, Top);
    }
}
