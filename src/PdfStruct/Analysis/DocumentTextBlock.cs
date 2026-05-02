// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace PdfStruct.Analysis;

/// <summary>
/// A <see cref="TextBlock"/> paired with the 1-indexed page number on which
/// it was extracted. Forms the document-wide input sequence consumed by
/// <see cref="IElementClassifier"/>: classifiers see every block in reading
/// order across the whole document, with page boundaries preserved on each
/// element rather than implied by the call shape. This shape is what makes
/// neighbour-relative signals (previous/next style contrast, vertical gap,
/// next-block-on-different-page penalties) expressible without hidden
/// global state.
/// </summary>
/// <param name="PageNumber">1-indexed page number on which <paramref name="Block"/> was extracted.</param>
/// <param name="Block">The extracted text block, including font and layout signals.</param>
/// <param name="IsStatsOnly">
/// When <c>true</c>, the entry contributes to document-wide statistics
/// (font-size frequency, mode, rarity tables) but is <em>not</em> part of
/// the output stream and is <em>not</em> a candidate neighbour for
/// previous/next comparison. Used by the parser to inject synthesised
/// blocks that recover font-distribution mass absorbed by upstream
/// passes (for example, list lines pulled out of paragraph merging into
/// dedicated list elements). Classifiers must:
/// <list type="bullet">
///   <item><description>include these entries when computing document-wide statistics, and</description></item>
///   <item><description>emit no element for them, and</description></item>
///   <item><description>skip them when iterating to find a previous or next neighbour.</description></item>
/// </list>
/// Failure to skip stats-only entries during neighbour iteration would
/// pollute neighbour comparisons with synthesised blocks that do not
/// belong to the visible document flow.
/// </param>
/// <param name="PageWidth">
/// Width of the page the block sits on, in PDF points. Carried with the
/// block so layout signals that need the page's horizontal extent
/// (centre-alignment detection, normalised indent) can be computed
/// without the classifier reaching back to the parser. Defaults to
/// <c>0</c>, which consumers must treat as "page geometry not supplied"
/// and silently skip — short-form test fixtures construct
/// <see cref="DocumentTextBlock"/> without page geometry.
/// </param>
public readonly record struct DocumentTextBlock(
    int PageNumber,
    TextBlock Block,
    bool IsStatsOnly = false,
    double PageWidth = 0);
