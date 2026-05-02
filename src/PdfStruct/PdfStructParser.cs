// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;
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
/// One row of text-line diagnostic output, produced before paragraph merging.
/// </summary>
/// <param name="PageNumber">1-indexed page number where the line appears.</param>
/// <param name="Line">The extracted text line, including its line-level bounding box and style signals.</param>
public readonly record struct TextLineDiagnosticRow(
    int PageNumber,
    Analysis.TextBlock Line);

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

    /// <summary>
    /// Initializes with the specified options. The default classifier is a
    /// <see cref="CompositeElementClassifier"/> wrapping a single
    /// <see cref="FontBasedElementClassifier"/>; callers that want to inject
    /// pattern-driven heading recognition (for example a
    /// <see cref="RegexHeadingClassifier"/> in front of the font model) use
    /// the constructor that accepts a custom classifier instance.
    /// </summary>
    public PdfStructParser(PdfStructOptions options)
    {
        _options = options;
        _layoutAnalyzer = new XyCutLayoutAnalyzer(options.MinGapRatioX, options.MinGapRatioY);
        _classifier = new CompositeElementClassifier(
            new FontBasedElementClassifier(options.HeadingProbabilityThreshold));
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

    /// <summary>
    /// Extracts text lines before paragraph merging. Intended for pipeline
    /// diagnostics and debug overlays; it does not affect the public JSON
    /// schema.
    /// </summary>
    /// <param name="filePath">Path to the input PDF.</param>
    /// <returns>One row per pre-paragraph text line, in page extraction order.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public IReadOnlyList<TextLineDiagnosticRow> AnalyzeTextLines(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath);
        var rows = new List<TextLineDiagnosticRow>();
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            foreach (var line in ExtractPageTextLines(pdf.GetPage(p)))
                rows.Add(new TextLineDiagnosticRow(p, line.ToTextBlock()));
        }

        return rows;
    }

    /// <summary>Per-page extraction + neighbour-aware scoring for diagnostic output.</summary>
    private IReadOnlyList<HeadingDiagnosticRow> AnalyzeHeadingProbabilitiesInternal(UglyToad.PdfPig.PdfDocument pdf)
    {
        var documentBlocks = new List<DocumentTextBlock>();
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            var page = pdf.GetPage(p);
            foreach (var block in ExtractPageBlocks(page))
                documentBlocks.Add(new DocumentTextBlock(p, block, IsStatsOnly: false, PageWidth: page.Width));
        }

        var classifier = new FontBasedElementClassifier(_options.HeadingProbabilityThreshold);
        var entries = classifier.AnalyzeHeadings(documentBlocks);

        var rows = new List<HeadingDiagnosticRow>(entries.Count);
        foreach (var entry in entries)
        {
            var doc = documentBlocks[entry.Index];
            rows.Add(new HeadingDiagnosticRow(
                PageNumber: doc.PageNumber,
                Block: doc.Block,
                Breakdown: entry.Breakdown,
                ClassifiedAsHeading: entry.ClassifiedAsHeading));
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

        var pageLines = new Dictionary<int, IReadOnlyList<TextLineBlock>>(pdf.NumberOfPages);
        var pageGeometries = new Dictionary<int, PageGeometry>(pdf.NumberOfPages);
        var pageHeights = new Dictionary<int, double>(pdf.NumberOfPages);
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            var page = pdf.GetPage(p);
            pageGeometries[p] = new PageGeometry(page.Width, page.Height);
            pageHeights[p] = page.Height;
            pageLines[p] = ExtractPageTextLines(page);
        }

        if (_options.ExcludeHeadersFooters)
            pageLines = FilterRunningFurnitureLines(pageLines, pageGeometries);

        var originalPageLines = pageLines.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<TextLineBlock>)pair.Value.ToList());

        var pageLists = DetectListsPerPage(pageLines);

        if (pageLists.Count > 0)
            ApplyConservativeReconciliation(pageLines, originalPageLines, pageLists);

        var pageBlocks = new Dictionary<int, IReadOnlyList<TextBlock>>(pdf.NumberOfPages);
        var statsOnlyBlocks = new List<DocumentTextBlock>();
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            PageGeometry? pageGeometry = _options.ExcludeHeadersFooters ? pageGeometries[p] : null;
            var blocks = BuildPageBlocks(pageLines[p], pageGeometry);

            if (pageLists.TryGetValue(p, out var listsOnPage))
            {
                foreach (var list in listsOnPage)
                    foreach (var item in list.Items)
                        statsOnlyBlocks.Add(new DocumentTextBlock(
                            p, SynthesizeListItemStatsBlock(list, item), IsStatsOnly: true));

                var augmented = new List<TextBlock>(blocks.Count + listsOnPage.Count);
                augmented.AddRange(blocks);
                for (var i = 0; i < listsOnPage.Count; i++)
                    augmented.Add(MakeListPlaceholder(listsOnPage[i], p, i));
                var ordered = _layoutAnalyzer.DetermineReadingOrder(augmented);
                blocks = WithStandaloneFlag(ordered);
            }

            pageBlocks[p] = blocks;
        }

        var totalCount = statsOnlyBlocks.Count;
        foreach (var blocks in pageBlocks.Values) totalCount += blocks.Count;
        var documentBlocks = new List<DocumentTextBlock>(totalCount);
        for (var p = 1; p <= pdf.NumberOfPages; p++)
        {
            var pageWidth = pageGeometries[p].Width;
            foreach (var block in pageBlocks[p])
                documentBlocks.Add(new DocumentTextBlock(p, block, IsStatsOnly: false, PageWidth: pageWidth));
        }
        documentBlocks.AddRange(statsOnlyBlocks);

        var elementId = 1;
        var elements = _classifier.Classify(documentBlocks, ref elementId);
        doc.Kids.AddRange(elements);

        if (pageLists.Count > 0)
            ReplaceListPlaceholders(doc.Kids, pageLists, originalPageLines);

        TemplateClassConsistency.PromoteSharedTemplates(doc.Kids);

        var pageWidths = pageGeometries.ToDictionary(pair => pair.Key, pair => pair.Value.Width);
        AssignHeadingLevels(doc.Kids, pageWidths);

        if (_options.ExcludeHeadersFooters)
        {
            var repeatingIds = RunningFurnitureDetector.DetectRepeatingIds(doc.Kids, pageHeights);
            if (repeatingIds.Count > 0)
                doc.Kids.RemoveAll(e => repeatingIds.Contains(e.Id));
        }

        RenumberElements(doc.Kids);

        string? markdown = _options.Format.HasFlag(OutputFormat.Markdown)
            ? new MarkdownRenderer().Render(doc) : null;
        string? json = _options.Format.HasFlag(OutputFormat.Json)
            ? new JsonRenderer().Render(doc) : null;

        return new PdfStructResult(doc, markdown, json);
    }

    /// <summary>
    /// Assigns numeric heading levels 1..N to <see cref="Models.HeadingElement"/>
    /// instances by clustering them on typographic and layout style
    /// (font size, font name, derived bold flag, indent bucket, page
    /// alignment) and ordering the resulting groups from largest/heaviest to
    /// smallest/lightest. Levels are uncapped on the data model; the Markdown
    /// renderer clamps to H6 at output time.
    /// </summary>
    /// <remarks>
    /// Ports the OpenDataLoader-pdf <c>HeadingProcessor</c> level-assignment
    /// pass with two extensions: indent and alignment join the style key
    /// (so a document whose chapter/section/sub-section headings share font
    /// and weight but sit at distinct left margins can still cluster into
    /// distinct levels), and headings that arrive with a non-zero
    /// <see cref="Models.HeadingElement.HeadingLevel"/> are treated as
    /// already-authoritative and left unchanged. The latter preserves the
    /// hierarchy a pattern-driven classifier (e.g.
    /// <see cref="Analysis.RegexHeadingClassifier"/>) intentionally
    /// assigned per pattern.
    /// </remarks>
    private static void AssignHeadingLevels(
        List<Models.ContentElement> kids,
        IReadOnlyDictionary<int, double> pageWidths)
    {
        var unassigned = kids
            .OfType<Models.HeadingElement>()
            .Where(h => h.HeadingLevel == 0)
            .ToList();
        if (unassigned.Count == 0) return;

        var styleGroups = unassigned
            .GroupBy(h => BuildStyleKey(h, pageWidths))
            .OrderByDescending(g => g.Key.FontSize)
            .ThenByDescending(g => g.Key.IsBold)
            .ThenBy(g => g.Key.AlignmentRank)
            .ThenBy(g => g.Key.IndentBucket)
            .ThenBy(g => g.Key.FontName, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < styleGroups.Count; i++)
        {
            var level = i + 1;
            var label = HeadingLevelLabel(level);
            foreach (var heading in styleGroups[i])
            {
                heading.HeadingLevel = level;
                heading.Level = label;
            }
        }
    }

    /// <summary>
    /// Builds the composite style key used to group headings in
    /// <see cref="AssignHeadingLevels"/>. Font size, bold, and font name
    /// remain the primary axes; indent (rounded to a 5pt bucket) and
    /// alignment (centered vs left-aligned) are added so headings that
    /// share typography but differ in layout role end up in distinct
    /// groups — a sub-section indented one column further than its
    /// parent chapter, for example, or a centred document title above
    /// left-aligned section headings of the same font size.
    /// </summary>
    private static TextStyleKey BuildStyleKey(Models.HeadingElement heading, IReadOnlyDictionary<int, double> pageWidths)
    {
        var indentBucket = (int)Math.Round(heading.BoundingBox.Left / 5.0);
        var alignmentRank = ClassifyAlignment(heading, pageWidths);
        return new TextStyleKey(
            FontSize: heading.Text.FontSize,
            IsBold: IsBoldFontName(heading.Text.Font),
            FontName: heading.Text.Font,
            IndentBucket: indentBucket,
            AlignmentRank: alignmentRank);
    }

    /// <summary>
    /// Maps a heading's horizontal position on its page to a coarse rank:
    /// <c>0</c> for centred (both side margins substantial and roughly
    /// equal), <c>1</c> for left-aligned, <c>2</c> when page geometry is
    /// unknown. The rank doubles as the within-cluster sort order, so a
    /// centred title naturally precedes a left-aligned heading of the
    /// same font size when both groups need to be ranked.
    /// </summary>
    private static int ClassifyAlignment(
        Models.HeadingElement heading,
        IReadOnlyDictionary<int, double> pageWidths)
    {
        if (!pageWidths.TryGetValue(heading.PageNumber, out var pageWidth) || pageWidth <= 0)
            return 2;

        var leftMargin = heading.BoundingBox.Left;
        var rightMargin = pageWidth - heading.BoundingBox.Right;
        if (leftMargin <= 0 || rightMargin <= 0) return 1;

        var minMargin = pageWidth * 0.15;
        if (leftMargin < minMargin || rightMargin < minMargin) return 1;

        var asymmetry = Math.Abs(leftMargin - rightMargin) / pageWidth;
        return asymmetry < 0.05 ? 0 : 1;
    }

    /// <summary>Composite typographic-and-layout style key used for grouping headings.</summary>
    private readonly record struct TextStyleKey(double FontSize, bool IsBold, string FontName, int IndentBucket, int AlignmentRank);

    /// <summary>
    /// Heuristic bold detection from a font name. Mirrors the
    /// <see cref="TextLineBuilder.IsBold"/> derivation but is repeated here because
    /// only the rendered <see cref="Models.TextProperties.Font"/> string is
    /// preserved on the heading element.
    /// </summary>
    private static bool IsBoldFontName(string fontName) =>
        fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
        fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ||
        fontName.Contains("Black", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the structural label for a heading level. Mirrors OpenDataLoader-pdf's
    /// vocabulary: <c>Doctitle</c> for the document title (level 1) and <c>Subtitle</c>
    /// for every nested heading (level 2 and below). The numeric depth is carried by
    /// <see cref="Models.HeadingElement.HeadingLevel"/>; this string is the coarse
    /// semantic tag, not a depth encoding.
    /// </summary>
    private static string HeadingLevelLabel(int level) => level == 1 ? "Doctitle" : "Subtitle";

    /// <summary>
    /// Renumbers elements sequentially while preserving the order produced by
    /// the extraction and layout-analysis pipeline. Top-level elements are
    /// numbered 1..N first; nested children inside list items are then
    /// numbered with the next available identifier so every element in the
    /// document has a unique id.
    /// </summary>
    private static void RenumberElements(List<Models.ContentElement> elements)
    {
        var nextId = 1;
        foreach (var element in elements)
            element.Id = nextId++;

        foreach (var element in elements)
        {
            if (element is not Models.ListElement list) continue;
            foreach (var item in list.ListItems)
                foreach (var child in item.Kids)
                    child.Id = nextId++;
        }
    }

    /// <summary>
    /// Sentinel-text prefix used to inject list placeholders into the
    /// classifier-bound text-block stream. The placeholder is replaced with
    /// the actual <see cref="Models.ListElement"/> after classification.
    /// </summary>
    private const string ListPlaceholderPrefix = "PDFSTRUCT_LIST_PLACEHOLDER";

    /// <summary>
    /// Runs the Phase 1 list detector against each page's residual line
    /// stream, mutates the line stream to remove claimed lines, and
    /// returns the per-page detected list runs. Pages with no detected
    /// lists are absent from the returned dictionary.
    /// </summary>
    private static Dictionary<int, IReadOnlyList<DetectedList>> DetectListsPerPage(
        Dictionary<int, IReadOnlyList<TextLineBlock>> pageLines)
    {
        var result = new Dictionary<int, IReadOnlyList<DetectedList>>();
        foreach (var page in pageLines.Keys.ToList())
        {
            var detection = ListDetector.Detect(pageLines[page]);
            if (detection.Lists.Count == 0) continue;

            result[page] = detection.Lists;
            pageLines[page] = detection.ResidualLines;
        }
        return result;
    }

    /// <summary>
    /// Builds a <see cref="TextBlock"/> placeholder representing a detected
    /// list. The placeholder participates in the layout-analysis reading
    /// order alongside paragraph blocks; after classification it is
    /// recognised by its sentinel text and replaced with a real
    /// <see cref="Models.ListElement"/>.
    /// </summary>
    /// <summary>
    /// Enforces the structural invariants required of detector output by
    /// dropping any list whose bounding box overlaps with another list's
    /// bounding box, or whose bounding box substantially contains a
    /// provisional paragraph block. Lines claimed by dropped lists are
    /// returned to the page's residual line stream so they participate in
    /// the final paragraph merge.
    /// </summary>
    /// <remarks>
    /// "False negative is preferable to false positive": a list that
    /// cannot cleanly own its territory is not emitted at all. Phase 1 has
    /// no rescue mechanism that could absorb intervening content into a
    /// list's children, so the only safe response to a violation is to
    /// un-confirm the offending list.
    /// </remarks>
    private static void ApplyConservativeReconciliation(
        Dictionary<int, IReadOnlyList<TextLineBlock>> pageResidualLines,
        IReadOnlyDictionary<int, IReadOnlyList<TextLineBlock>> originalPageLines,
        Dictionary<int, IReadOnlyList<DetectedList>> pageLists)
    {
        foreach (var page in pageLists.Keys.ToList())
        {
            var lists = pageLists[page];
            var residualBlocks = MergeLinesIntoBlocks(pageResidualLines[page]);
            var rejected = IdentifyInvariantViolators(lists, residualBlocks);
            if (rejected.Count == 0) continue;

            var kept = lists.Where(list => !rejected.Contains(list)).ToList();
            var keptClaimed = new HashSet<int>();
            foreach (var list in kept)
                foreach (var item in list.Items)
                    foreach (var idx in item.ClaimedLineIndices)
                        keptClaimed.Add(idx);

            pageResidualLines[page] = originalPageLines[page]
                .Where((_, index) => !keptClaimed.Contains(index))
                .ToList();

            if (kept.Count == 0)
                pageLists.Remove(page);
            else
                pageLists[page] = kept;
        }
    }

    /// <summary>
    /// Identifies which confirmed lists violate the structural invariants
    /// of detector output: pairwise sibling-list bounding-box disjointness,
    /// and the absence of any provisional paragraph block substantially
    /// contained within a list's bounding box.
    /// </summary>
    private static HashSet<DetectedList> IdentifyInvariantViolators(
        IReadOnlyList<DetectedList> lists,
        IReadOnlyList<TextBlock> provisionalParagraphs)
    {
        var rejected = new HashSet<DetectedList>();

        for (var i = 0; i < lists.Count; i++)
        {
            for (var j = i + 1; j < lists.Count; j++)
            {
                if (lists[i].BoundingBox.Overlaps(lists[j].BoundingBox))
                {
                    rejected.Add(lists[i]);
                    rejected.Add(lists[j]);
                }
            }
        }

        foreach (var list in lists)
        {
            if (rejected.Contains(list)) continue;
            foreach (var paragraph in provisionalParagraphs)
            {
                if (BoundingBoxSubstantiallyContains(list.BoundingBox, paragraph.BoundingBox))
                {
                    rejected.Add(list);
                    break;
                }
            }
        }

        return rejected;
    }

    /// <summary>
    /// Returns <c>true</c> when at least 80% of <paramref name="inner"/>'s
    /// area falls inside <paramref name="container"/>. Used by the
    /// reconciliation pass to detect paragraphs that share a list's
    /// bounding-box interior.
    /// </summary>
    private static bool BoundingBoxSubstantiallyContains(Models.BoundingBox container, Models.BoundingBox inner)
    {
        var overlapLeft = Math.Max(container.Left, inner.Left);
        var overlapRight = Math.Min(container.Right, inner.Right);
        var overlapBottom = Math.Max(container.Bottom, inner.Bottom);
        var overlapTop = Math.Min(container.Top, inner.Top);
        if (overlapRight <= overlapLeft || overlapTop <= overlapBottom) return false;

        var overlapArea = (overlapRight - overlapLeft) * (overlapTop - overlapBottom);
        var innerArea = inner.Width * inner.Height;
        return innerArea > 0 && overlapArea / innerArea >= 0.8;
    }

    /// <summary>
    /// Synthesises a body-typical <see cref="TextBlock"/> per detected list
    /// item, used only as input to <see cref="DocumentStatistics"/>. The
    /// placeholder block (different concept, see
    /// <see cref="MakeListPlaceholder"/>) is what flows through the
    /// classifier; this stats block exists to keep document-wide font
    /// statistics close to the pre-detection distribution so that paragraph
    /// vs heading classification on unrelated blocks is not destabilised
    /// by the act of removing list lines from the paragraph merge.
    /// </summary>
    private static TextBlock SynthesizeListItemStatsBlock(DetectedList list, DetectedListItem item) => new(
        item.BoundingBox,
        item.Body,
        list.FontName,
        list.FontSize,
        IsBold: false,
        LineCount: 1,
        FirstLineLeft: item.BoundingBox.Left,
        MedianLineLeft: item.BoundingBox.Left,
        LastLineLeft: item.BoundingBox.Left,
        FirstLineRight: item.BoundingBox.Right,
        MedianLineRight: item.BoundingBox.Right,
        LastLineRight: item.BoundingBox.Right);

    private static TextBlock MakeListPlaceholder(DetectedList list, int pageNumber, int indexOnPage)
    {
        var marker = $"{ListPlaceholderPrefix}{pageNumber}{indexOnPage}";
        return new TextBlock(
            list.BoundingBox,
            marker,
            list.FontName,
            list.FontSize,
            IsBold: false,
            LineCount: list.Items.Count,
            FirstLineLeft: list.BoundingBox.Left,
            MedianLineLeft: list.BoundingBox.Left,
            LastLineLeft: list.BoundingBox.Left,
            FirstLineRight: list.BoundingBox.Right,
            MedianLineRight: list.BoundingBox.Right,
            LastLineRight: list.BoundingBox.Right) with
        { IsStandalone = false };
    }

    /// <summary>
    /// Walks the document's element list and replaces every placeholder
    /// element (recognised by sentinel text content) with the
    /// corresponding <see cref="Models.ListElement"/>. Both
    /// <see cref="Models.ParagraphElement"/> and
    /// <see cref="Models.HeadingElement"/> are handled because the
    /// classifier may resolve the sentinel block into either type.
    /// </summary>
    private static void ReplaceListPlaceholders(
        List<Models.ContentElement> kids,
        Dictionary<int, IReadOnlyList<DetectedList>> pageLists,
        IReadOnlyDictionary<int, IReadOnlyList<TextLineBlock>> originalPageLines)
    {
        for (var i = 0; i < kids.Count; i++)
        {
            var element = kids[i];
            var content = element switch
            {
                Models.ParagraphElement p => p.Text.Content,
                Models.HeadingElement h => h.Text.Content,
                _ => null
            };
            if (content is null) continue;
            if (!content.StartsWith(ListPlaceholderPrefix, StringComparison.Ordinal)) continue;

            var rest = content[ListPlaceholderPrefix.Length..];
            var sep = rest.IndexOf('');
            if (sep < 0) continue;
            if (!int.TryParse(rest.AsSpan(0, sep), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pageNumber)) continue;
            if (!int.TryParse(rest.AsSpan(sep + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var indexOnPage)) continue;
            if (!pageLists.TryGetValue(pageNumber, out var lists)) continue;
            if (indexOnPage < 0 || indexOnPage >= lists.Count) continue;
            if (!originalPageLines.TryGetValue(pageNumber, out var pageLines)) continue;

            kids[i] = BuildListElement(lists[indexOnPage], pageNumber, element.Id, pageLines);
        }
    }

    /// <summary>
    /// Materialises a <see cref="Models.ListElement"/> from a detector
    /// output. The numbering style is fixed at <c>"ordered"</c> for Phase 1
    /// and Phase 2 (Arabic-numeric labels only). Per-item children, if any
    /// were absorbed by the territory walk (Phase 2 § 6), are formed here
    /// by feeding each item's child line indices through the same
    /// paragraph-merger function the page-level pipeline uses, then
    /// wrapping each resulting block as a paragraph element child of the
    /// list item. Child element identifiers are assigned later by
    /// <see cref="RenumberElements"/>.
    /// </summary>
    private static Models.ListElement BuildListElement(
        DetectedList list,
        int pageNumber,
        int id,
        IReadOnlyList<TextLineBlock> pageLines)
    {
        var element = new Models.ListElement
        {
            Id = id,
            PageNumber = pageNumber,
            BoundingBox = list.BoundingBox,
            NumberingStyle = "ordered",
            NumberOfListItems = list.Items.Count
        };
        foreach (var item in list.Items)
        {
            var listItem = new Models.ListItem
            {
                BoundingBox = item.BoundingBox,
                PageNumber = pageNumber,
                Text = new Models.TextProperties
                {
                    Content = item.Body,
                    FontSize = list.FontSize,
                    Font = list.FontName
                }
            };

            if (item.ChildrenLineIndices.Count > 0)
            {
                var childLines = item.ChildrenLineIndices
                    .Select(idx => pageLines[idx])
                    .ToList();
                var childBlocks = MergeLinesIntoBlocks(childLines);
                foreach (var block in childBlocks)
                {
                    listItem.Kids.Add(new Models.ParagraphElement
                    {
                        PageNumber = pageNumber,
                        BoundingBox = block.BoundingBox,
                        Text = new Models.TextProperties
                        {
                            Content = block.Text,
                            Font = block.FontName,
                            FontSize = block.FontSize
                        }
                    });
                }
            }

            element.ListItems.Add(listItem);
        }
        return element;
    }

    private static Dictionary<int, IReadOnlyList<TextLineBlock>> FilterRunningFurnitureLines(
        IReadOnlyDictionary<int, IReadOnlyList<TextLineBlock>> pageLines,
        IReadOnlyDictionary<int, PageGeometry> pageGeometries)
    {
        if (pageLines.Count < 3) return pageLines.ToDictionary(pair => pair.Key, pair => pair.Value);

        var candidates = new List<RunningLineCandidate>();
        foreach (var (pageNumber, lines) in pageLines)
        {
            if (!pageGeometries.TryGetValue(pageNumber, out var pageGeometry))
                continue;

            for (var index = 0; index < lines.Count; index++)
            {
                var band = ClassifyRunningFurnitureBand(lines[index].BoundingBox, pageGeometry);
                if (band is null) continue;

                var normalized = NormalizeRunningFurnitureText(lines[index].Text);
                if (string.IsNullOrWhiteSpace(normalized)) continue;

                var quantisedLeft = Math.Round(lines[index].BoundingBox.Left / 10.0) * 10.0;
                candidates.Add(new RunningLineCandidate(pageNumber, index, band.Value, normalized, quantisedLeft));
            }
        }

        var repeatedHeaderFooter = candidates
            .Where(candidate => candidate.Band is not RunningFurnitureBand.Side);

        var minPagesForRepeat = Math.Max(2, (int)Math.Ceiling(pageLines.Count * RunningFurnitureDetector.RepeatRatioThreshold));
        // Group by position too (quantised to 10pt buckets) — two lines that share
        // text content but appear at very different lefts are not the same running
        // element. Without this, a centred document title is removed alongside a
        // recurring page header that happens to share the same text.
        var rejected = repeatedHeaderFooter
            .GroupBy(candidate => (candidate.Band, candidate.NormalizedText, candidate.QuantisedLeft))
            .Where(group => group.Select(candidate => candidate.PageNumber).Distinct().Count() >= minPagesForRepeat)
            .SelectMany(group => group.Select(candidate => (candidate.PageNumber, candidate.LineIndex)))
            .ToHashSet();

        foreach (var candidate in candidates.Where(candidate => candidate.Band is RunningFurnitureBand.Side))
            rejected.Add((candidate.PageNumber, candidate.LineIndex));

        if (rejected.Count == 0)
            return pageLines.ToDictionary(pair => pair.Key, pair => pair.Value);

        return pageLines.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<TextLineBlock>)pair.Value
                .Where((_, index) => !rejected.Contains((pair.Key, index)))
                .ToList());
    }

    private static RunningFurnitureBand? ClassifyRunningFurnitureBand(Models.BoundingBox bbox, PageGeometry pageGeometry)
    {
        if (pageGeometry.Height <= 0 || pageGeometry.Width <= 0)
            return null;

        var bottomRatio = bbox.Bottom / pageGeometry.Height;
        var topRatio = bbox.Top / pageGeometry.Height;
        if (topRatio < RunningFurnitureDetector.FooterBandBottomRatio)
            return RunningFurnitureBand.Footer;
        if (bottomRatio > 1.0 - RunningFurnitureDetector.HeaderBandTopRatio)
            return RunningFurnitureBand.Header;

        var nearLeftOrRightEdge = bbox.Right <= pageGeometry.Width * 0.12
            || bbox.Left >= pageGeometry.Width * 0.88;
        var narrowAndTall = bbox.Width <= SideFurnitureMaxWidth(pageGeometry)
            && bbox.Height >= pageGeometry.Height * 0.15;
        return nearLeftOrRightEdge && narrowAndTall
            ? RunningFurnitureBand.Side
            : null;
    }

    private static bool IsSideFurnitureBlock(Models.BoundingBox bbox, PageGeometry pageGeometry)
    {
        if (pageGeometry.Height <= 0 || pageGeometry.Width <= 0)
            return false;

        var nearLeftOrRightEdge = bbox.Right <= pageGeometry.Width * 0.12
            || bbox.Left >= pageGeometry.Width * 0.88;
        return nearLeftOrRightEdge
            && bbox.Width <= SideFurnitureMaxWidth(pageGeometry)
            && bbox.Height >= pageGeometry.Height * 0.08;
    }

    private static double SideFurnitureMaxWidth(PageGeometry pageGeometry) =>
        Math.Max(16.0, Math.Min(28.0, pageGeometry.Width * 0.08));

    private static string NormalizeRunningFurnitureText(string text)
    {
        var normalized = s_digitRun.Replace(text.Trim(), "#");
        normalized = s_whitespace.Replace(normalized, " ");
        return normalized;
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
        var lines = ExtractPageTextLines(page);
        PageGeometry? pageGeometry = _options.ExcludeHeadersFooters ? new PageGeometry(page.Width, page.Height) : null;
        return BuildPageBlocks(lines, pageGeometry);
    }

    private IReadOnlyList<TextBlock> BuildPageBlocks(
        IReadOnlyList<TextLineBlock> lines,
        PageGeometry? pageGeometry = null)
    {
        if (lines.Count == 0) return [];

        var orderedLines = DetermineTextLineReadingOrder(lines);
        var textBlocks = MergeLinesIntoBlocks(orderedLines);
        if (pageGeometry is { } geometry)
        {
            textBlocks = textBlocks
                .Where(block => !IsSideFurnitureBlock(block.BoundingBox, geometry))
                .ToList();
            if (textBlocks.Count == 0) return [];
        }

        var ordered = _layoutAnalyzer.DetermineReadingOrder(textBlocks);
        return WithStandaloneFlag(ordered);
    }

    private IReadOnlyList<TextLineBlock> DetermineTextLineReadingOrder(IReadOnlyList<TextLineBlock> lines)
    {
        if (lines.Count <= 1) return lines;

        var lineBlocks = lines.Select(line => line.ToTextBlock()).ToList();
        var lineByBlock = new Dictionary<TextBlock, TextLineBlock>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < lineBlocks.Count; i++)
            lineByBlock[lineBlocks[i]] = lines[i];

        return _layoutAnalyzer.DetermineReadingOrder(lineBlocks)
            .Select(block => lineByBlock[block])
            .ToList();
    }

    /// <summary>
    /// Extracts text lines for a page before paragraph merging. This keeps the
    /// word-to-line stage separate from the later line-to-block stage.
    /// </summary>
    private IReadOnlyList<TextLineBlock> ExtractPageTextLines(Page page)
    {
        var words = page.GetWords(LetterGrouper.Instance).ToList();
        if (words.Count == 0) return [];

        var lines = GroupWordsIntoLines(words);
        if (_options.FilterHiddenText)
            lines = FilterTextLines(lines, page.Width, page.Height);

        return ProcessTextLines(lines);
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

    private List<TextLineBlock> ProcessTextLines(IReadOnlyList<TextLineBlock> lines)
    {
        var lineBlocks = lines.Select(l => l.ToTextBlock()).ToList();
        var processed = TextSanitizer.ProcessBlocks(
            lineBlocks,
            _options.SanitizeText,
            _options.InvalidCharacterReplacement,
            _options.SanitizationRules);

        var result = new List<TextLineBlock>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
            result.Add(lines[i] with { Text = processed[i].Text });

        return result;
    }

    private static List<TextLineBlock> FilterTextLines(
        IReadOnlyList<TextLineBlock> lines,
        double pageWidth,
        double pageHeight)
    {
        return lines.Where(line =>
        {
            if (line.FontSize < 1.0) return false;
            if (string.IsNullOrWhiteSpace(line.Text)) return false;
            if (line.BoundingBox.Right < 0 || line.BoundingBox.Left > pageWidth) return false;
            if (line.BoundingBox.Top < 0 || line.BoundingBox.Bottom > pageHeight) return false;
            return true;
        }).ToList();
    }

    /// <summary>
    /// Groups a page's words into baseline-aligned text lines using a two-phase
    /// relative-threshold strategy.
    /// </summary>
    /// <remarks>
    /// Phase 1 clusters words by baseline proximity only — words that share a
    /// vertical band within half the local glyph height belong to the same
    /// "raw line" regardless of horizontal distance. Phase 2 then walks each
    /// raw line left-to-right and splits at any gap that exceeds an adaptive
    /// threshold: <c>min(medianGap × 3, avgHeight × 4)</c>. The median-relative
    /// arm catches column boundaries (a rare wide gap among many tight ones in
    /// a multi-column row) while the height-relative arm provides a floor for
    /// pathological cases where the median is unhelpful (single-word raw
    /// lines, lines with all-tiny intra-glyph kerning gaps).
    ///
    /// <para>
    /// The earlier implementation used an absolute <c>min(35, max(15, h * 1.5))</c>
    /// gap cap. That worked for normal body text but split justified
    /// short-line paragraphs (inter-word gaps just over 15pt) and
    /// letter-spaced display titles like <c>MY DEAREST</c> (a 48pt
    /// title-internal gap). The relative thresholds keep both cases on a
    /// single line while still cutting at multi-column boundaries.
    /// </para>
    /// </remarks>
    private static List<TextLineBlock> GroupWordsIntoLines(List<Word> words)
    {
        if (words.Count == 0) return [];

        var sorted = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var rawLines = new List<List<Word>>();
        var currentRawLine = new List<Word> { sorted[0] };
        var currentBaselineY = Math.Min(sorted[0].BoundingBox.Bottom, sorted[0].BoundingBox.Top);
        var currentMaxHeight = Math.Abs(sorted[0].BoundingBox.Top - sorted[0].BoundingBox.Bottom);
        var currentMaxFontSize = WordFontSize(sorted[0]);

        for (int i = 1; i < sorted.Count; i++)
        {
            var w = sorted[i];
            var wLowerY = Math.Min(w.BoundingBox.Bottom, w.BoundingBox.Top);
            var yDist = Math.Abs(wLowerY - currentBaselineY);
            var wHeight = Math.Abs(w.BoundingBox.Top - w.BoundingBox.Bottom);
            var wFontSize = WordFontSize(w);

            // Two same-baseline words can have very close `Bottom` values
            // even when they belong to a different visual line — the canonical
            // case is a small-font body row in one column with its baseline
            // 1–2pt off from a large-font heading row in the other column.
            // Y proximity alone happily merges those, then Phase 2 sees the
            // body's tight median gap and incorrectly splits the heading's
            // own intra-word gap. Reject the merge whenever font sizes
            // diverge by more than ~50% of the larger size, which cleanly
            // separates body from heading without rejecting punctuation
            // (the bbox height of a comma is much smaller than a letter,
            // but they share the same point size).
            var maxFontSize = Math.Max(currentMaxFontSize, wFontSize);
            var fontSizeDiffRatio = maxFontSize > 0
                ? Math.Abs(currentMaxFontSize - wFontSize) / maxFontSize
                : 0.0;
            var sameLine = yDist <= currentMaxHeight * 0.5 && fontSizeDiffRatio <= 0.5;

            if (sameLine)
            {
                currentRawLine.Add(w);
                currentMaxHeight = Math.Max(currentMaxHeight, wHeight);
                currentMaxFontSize = Math.Max(currentMaxFontSize, wFontSize);
            }
            else
            {
                rawLines.Add(currentRawLine);
                currentRawLine = new List<Word> { w };
                currentBaselineY = wLowerY;
                currentMaxHeight = wHeight;
                currentMaxFontSize = wFontSize;
            }
        }
        rawLines.Add(currentRawLine);

        var lines = new List<TextLineBlock>();
        foreach (var rawLine in rawLines)
        {
            var byX = rawLine.OrderBy(w => w.BoundingBox.Left).ToList();
            var splitIndices = FindOutlierGapSplits(byX);
            var start = 0;
            foreach (var splitIndex in splitIndices)
            {
                lines.Add(BuildTextLineBlock(byX, start, splitIndex + 1));
                start = splitIndex + 1;
            }
            lines.Add(BuildTextLineBlock(byX, start, byX.Count));
        }

        return lines;
    }

    /// <summary>
    /// Hard ceiling, in PDF user-space points, on a single intra-line gap.
    /// Any gap above this is treated as a column boundary regardless of
    /// the line's local statistics. The cap exists for sparse single-row
    /// layouts (e.g. magazine table-of-contents pages where two large
    /// page-number badges sit on the same baseline at opposite ends of
    /// the page) — those rows have only one gap, so the median-relative
    /// rule would never split them. 100pt comfortably covers a
    /// 30pt-font letter-spaced heading (typical title-internal gaps stay
    /// under ~50pt) while still cutting page-number badges separated by
    /// hundreds of points.
    /// </summary>
    private const double MaxIntraLineGapPoints = 100.0;

    /// <summary>
    /// Returns the indices in <paramref name="wordsByX"/> after which a column-like
    /// outlier gap appears. A gap qualifies when it exceeds three times the
    /// raw-line median gap, four times the line's average glyph height, or
    /// the absolute <see cref="MaxIntraLineGapPoints"/> ceiling — whichever
    /// yields the lowest threshold. The three arms cooperate: the
    /// median-relative arm catches narrow-column layouts where the absolute
    /// gap is small but anomalous, the height-relative arm covers single-gap
    /// raw lines whose median equals the gap itself, and the absolute cap
    /// handles sparse rows whose font size is so large that the relative
    /// arms overshoot any plausible same-line gap.
    /// </summary>
    private static List<int> FindOutlierGapSplits(IReadOnlyList<Word> wordsByX)
    {
        if (wordsByX.Count <= 1) return [];

        var gaps = new double[wordsByX.Count - 1];
        for (var i = 0; i < gaps.Length; i++)
        {
            gaps[i] = Math.Max(
                0,
                wordsByX[i + 1].BoundingBox.Left - wordsByX[i].BoundingBox.Right);
        }

        var sortedGaps = (double[])gaps.Clone();
        Array.Sort(sortedGaps);
        var medianGap = sortedGaps[sortedGaps.Length / 2];

        var avgHeight = wordsByX.Average(w => Math.Abs(w.BoundingBox.Top - w.BoundingBox.Bottom));
        var threshold = Math.Min(
            Math.Min(medianGap * 3.0, avgHeight * 4.0),
            MaxIntraLineGapPoints);

        var splits = new List<int>();
        for (var i = 0; i < gaps.Length; i++)
        {
            if (gaps[i] > threshold)
            {
                splits.Add(i);
            }
        }
        return splits;
    }

    /// <summary>
    /// Constructs a <see cref="TextLineBlock"/> from a half-open slice
    /// <c>[start, endExclusive)</c> of <paramref name="words"/>, using the
    /// existing <see cref="TextLineBuilder"/> aggregator to keep bounding-box
    /// and font-statistic computation in one place.
    /// </summary>
    private static TextLineBlock BuildTextLineBlock(IReadOnlyList<Word> words, int start, int endExclusive)
    {
        var builder = new TextLineBuilder(words[start]);
        for (var i = start + 1; i < endExclusive; i++)
        {
            builder.Add(words[i]);
        }
        return builder.ToTextLineBlock();
    }

    /// <summary>
    /// Returns the point size of <paramref name="word"/>'s first letter,
    /// or zero when the word carries no letter information. Word font size
    /// is uniform within a <see cref="LetterGrouper"/>-produced word, so
    /// the first-letter sample is sufficient and cheaper than averaging.
    /// </summary>
    private static double WordFontSize(Word word) =>
        word.Letters.Count > 0 ? word.Letters[0].PointSize : 0.0;

    private static List<TextBlock> MergeLinesIntoBlocks(IReadOnlyList<TextLineBlock> lines)
    {
        if (lines.Count == 0) return [];

        var blocks = new List<List<TextLineBlock>>();
        var current = new List<TextLineBlock> { lines[0] };
        for (var i = 1; i < lines.Count; i++)
        {
            var curr = lines[i];
            if (ShouldMergeWithCurrentBlock(current, curr))
            {
                current.Add(curr);
            }
            else
            {
                blocks.Add(current);
                current = [curr];
            }
        }
        blocks.Add(current);

        return blocks.Select(MergeLines).ToList();
    }

    private static bool ShouldMergeWithCurrentBlock(IReadOnlyList<TextLineBlock> currentBlock, TextLineBlock next)
    {
        var previous = currentBlock[^1];
        if (!IsSameFontSize(previous, next))
            return false;

        if (previous.IsBold != next.IsBold)
            return false;

        if (!IsSameFontFace(previous, next))
            return false;

        if (!AreHorizontallyOverlapping(previous, next))
            return false;

        var gap = previous.Bottom - next.Top;
        if (gap < -Math.Max(previous.AvgHeight, next.AvgHeight) * 0.25)
            return false;

        var avgHeight = Math.Max(previous.AvgHeight, next.AvgHeight);
        var continues = SentenceFlow.IsLineContinuation(previous.Text);
        var maxGap = avgHeight * (continues ? 2.2 : 1.35);
        if (gap > maxGap)
            return false;

        var sameLeft = Math.Abs(previous.Left - next.Left) <= Math.Max(6.0, avgHeight * 0.75);
        var sameRight = Math.Abs(previous.Right - next.Right) <= Math.Max(8.0, avgHeight);
        if (!sameLeft && next.Width > previous.Width * 2.5)
            return false;

        var lineOverlap = HorizontalOverlapRatio(previous, next);
        return sameLeft || sameRight || (continues && lineOverlap >= 0.65);
    }

    private static bool AreHorizontallyOverlapping(TextLineBlock a, TextLineBlock b) =>
        HorizontalOverlapRatio(a, b) >= 0.35;

    private static double HorizontalOverlapRatio(TextLineBlock a, TextLineBlock b)
    {
        var overlap = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
        var minWidth = Math.Min(a.Width, b.Width);
        return minWidth > 0 ? overlap / minWidth : 0;
    }

    private static TextBlock MergeLines(List<TextLineBlock> lines)
    {
        var text = string.Join("\n", lines.Select(l => l.Text));
        var bbox = lines.Select(l => l.BoundingBox).Aggregate((a, b) => a.Merge(b));
        var first = lines[0];
        var last = lines[^1];

        var sortedLefts = lines.Select(l => l.Left).OrderBy(v => v).ToArray();
        var sortedRights = lines.Select(l => l.Right).OrderBy(v => v).ToArray();
        var medianLeft = sortedLefts[sortedLefts.Length / 2];
        var medianRight = sortedRights[sortedRights.Length / 2];

        return new TextBlock(
            bbox,
            text,
            first.FontName,
            first.FontSize,
            first.IsBold,
            LineCount: lines.Count,
            FirstLineLeft: first.Left,
            MedianLineLeft: medianLeft,
            LastLineLeft: last.Left,
            FirstLineRight: first.Right,
            MedianLineRight: medianRight,
            LastLineRight: last.Right,
            IsItalic: first.IsItalic,
            FontWeight: first.FontWeight);
    }

    /// <summary>Returns <c>true</c> when two lines have effectively the same font size (within 10% or 1pt).</summary>
    private static bool IsSameFontSize(TextLineBlock a, TextLineBlock b)
    {
        var delta = Math.Abs(a.FontSize - b.FontSize);
        var tolerance = Math.Max(1.0, 0.1 * Math.Max(a.FontSize, b.FontSize));
        return delta <= tolerance;
    }

    /// <summary>
    /// Returns <c>true</c> when two lines use the same font face. Compares
    /// names with the PDF subset prefix stripped — embedded subset fonts
    /// carry a synthetic six-uppercase-letter tag (for example
    /// <c>INPILL+HCRDotum</c>) that varies between embedding passes and
    /// must be ignored. A bold face and its regular sibling
    /// (<c>HCRDotum-Bold</c> vs <c>HCRDotum</c>) deliberately do <em>not</em>
    /// match — splitting on weight changes is what keeps a bold title
    /// from being merged into the regular-weight body that follows it.
    /// </summary>
    private static bool IsSameFontFace(TextLineBlock a, TextLineBlock b) =>
        StripSubsetPrefix(a.FontName) == StripSubsetPrefix(b.FontName);

    /// <summary>
    /// Strips the six-uppercase-letter subset prefix and trailing <c>+</c>
    /// from a PDF font name, leaving the underlying face identifier. Returns
    /// the input unchanged when no prefix is present.
    /// </summary>
    private static string StripSubsetPrefix(string fontName)
    {
        if (fontName.Length <= 7 || fontName[6] != '+')
            return fontName;

        for (var i = 0; i < 6; i++)
        {
            var c = fontName[i];
            if (c < 'A' || c > 'Z') return fontName;
        }
        return fontName[7..];
    }

    private static readonly Regex s_digitRun = new(@"\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex s_whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly record struct PageGeometry(double Width, double Height);

    private readonly record struct RunningLineCandidate(
        int PageNumber,
        int LineIndex,
        RunningFurnitureBand Band,
        string NormalizedText,
        double QuantisedLeft);

    private enum RunningFurnitureBand { Header, Footer, Side }

    private sealed class TextLineBuilder
    {
        private readonly List<Word> _words = [];

        public TextLineBuilder(Word w) => _words.Add(w);
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

        // Authoritative typographic flags come from PdfPig's parsed FontDetails
        // (set during font dictionary parsing). The fallback to font-name
        // substring matching only fires when FontDetails is unavailable, which
        // happens for synthetic glyph streams without an embedded font
        // descriptor.
        private UglyToad.PdfPig.PdfFonts.FontDetails? FirstFontDetails =>
            _words[0].Letters.FirstOrDefault()?.FontDetails;

        public bool IsBold => FirstFontDetails?.IsBold
            ?? FontName.Contains("Bold", StringComparison.OrdinalIgnoreCase)
            || FontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase)
            || FontName.Contains("Black", StringComparison.OrdinalIgnoreCase);

        public bool IsItalic => FirstFontDetails?.IsItalic
            ?? FontName.Contains("Italic", StringComparison.OrdinalIgnoreCase)
            || FontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

        public int FontWeight => FirstFontDetails?.Weight ?? (IsBold ? 700 : 400);

        public string Text => string.Join(" ", _words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));

        public Models.BoundingBox Bbox => new(Left, Bottom, Right, Top);

        public TextLineBlock ToTextLineBlock() => new(
            Bbox,
            Text,
            FontName,
            AvgFontSize,
            IsBold,
            BaselineY,
            AvgHeight,
            IsItalic: IsItalic,
            FontWeight: FontWeight);
    }
}
