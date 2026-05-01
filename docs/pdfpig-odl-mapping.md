# PdfPig ↔ upstream-pipeline data-model mapping

Status: draft for review (2026-05-01)
Scope: data-model audit needed by the Phase 1 list-detection implementation. Records what each text-line carrier exposes, where the upstream pipeline's expected fields come from on the PdfPig side, and which gaps Phase 1 must fill.

This document does not reproduce upstream code or upstream identifier names; it describes the data each side carries.

## Per-page text line — field mapping

The list detector consumes a per-page sequence of text lines. The table below lists every property the spec (`docs/list-detection-spec.md`, §§ 2 and 7) needs from each line, and traces it through the pipeline.

| Property the detector needs | What it means | PdfPig source | Current PdfStruct internal carrier | Gap for Phase 1? |
| --- | --- | --- | --- | --- |
| Text content | The visible string of the line | `Word.Text` joined per line by our line grouper | `TextLineBlock.Text` | none |
| Bounding box | left / right / top / bottom in user space | union of `Word.BoundingBox` | `TextLineBlock.BoundingBox` | none |
| Predominant font size | Average pt height of glyphs in the line | `Letter.PointSize` averaged per word | `TextLineBlock.FontSize` | none |
| Baseline y | y-coordinate of the line's baseline | `Letter.StartBaseLine.Y` | `TextLineBlock.BaselineY` | none |
| Average glyph height | Used to derive "typical inter-line spacing" for continuation absorption | derived from `Letter.GlyphRectangle` | `TextLineBlock.AvgHeight` | none |
| Hidden flag | Detector skips hidden lines without considering them | applied upstream by our hidden-text filter (`PromptInjectionFilter`) | hidden lines are removed from the line stream before the detector runs | none — handled by upstream filter |
| Claimed-by-list flag | Tells the paragraph merger and any subsequent list pass that this line is already owned | n/a (synthesised by the list detector) | not present — Phase 1 must add this | **yes — add `IsClaimedByList`** to `TextLineBlock` (or carry equivalent state in a side-set) |
| Glyph-level x of body text | Used by upstream to compute the precise body-indent of an item, separating "label width" from "line left" | `Letter.GlyphRectangle.Left` of the first non-label letter | not exposed by `TextLineBlock` | **deferred** — Phase 1 derives indent from line.Left and accepts the slight bbox slack on the body part |

## Per-page list element — output mapping

The detector emits a list aggregate plus item children. The Phase 1 model maps to PdfStruct's existing element hierarchy as follows; details (file paths, JSON renderer changes, fixture asserts) are owned by the implementation and are intentionally not pinned here.

| Output concept | Equivalent PdfStruct element | ODL JSON `level` and `heading level` | Phase 1 status |
| --- | --- | --- | --- |
| List aggregate (`numbering style`, `common prefix`, `common suffix`, items[]) | new `LabeledListElement` (sibling of `ParagraphElement`) | `"List"` (no `heading level`) | new type — Phase 1 must add |
| List item (label number, body text, body bbox, source line index) | new `ListItemElement` (child of the aggregate, addressable for citations) | `"List Item"` | new type — Phase 1 must add |

The Markdown renderer must emit ordered Markdown for an arabic-numeric list. The JSON renderer must produce the ODL-compatible `level: "List"` / `level: "List Item"` shape; field naming follows existing renderer conventions (e.g., `bounding box` array, `text content`, `page number`). Field names are not respecified here — they reuse what is already proven by the paragraph renderer.

## Pipeline placement

Today the per-page flow is:

```
words → group into lines → filter hidden text → sanitize → filter running furniture
         → merge lines into blocks (paragraph) → XY-Cut → classify → renumber
```

After Phase 1 it becomes:

```
words → group into lines → filter hidden text → sanitize → filter running furniture
         → detect arabic lists                                   ← NEW
         → merge unclaimed lines into blocks (paragraph)
         → XY-Cut over (paragraphs ∪ list aggregates)
         → classify
         → renumber
```

The list detector consumes the same `IReadOnlyList<TextLineBlock>` the paragraph merger does today; the merger receives the residual line set (claimed lines removed). The XY-Cut analyzer is unchanged: it sorts whatever blocks it is handed, regardless of whether they are paragraphs or list aggregates, since both expose the same `BoundingBox`-bearing protocol.

## Unfilled gaps recorded for later phases

These are not Phase 1 gaps; they are noted here so a later phase does not re-audit:

* Per-line per-glyph leading edge (needed for tighter body bbox once Phase 2/3 adds Roman / alphabetic detection where the label width is variable).
* Line-level alignment classification (justify / left / right / center) — needed by post-paragraph list rescue and by the upstream-style multi-pass paragraph merge that we have not yet ported.
* Cross-page line linkage — needed by the cross-page list joining phase.
* Bullet glyph set — needed by unordered list detection. Phase 1 makes no assumption about which glyphs are bullets.

## What Phase 1 changes in PdfStruct's data model

1. `TextLineBlock` gains a `bool IsClaimedByList` flag.
2. `ContentElement` hierarchy gains two leaf types: a list aggregate and a list item. The aggregate's children are list items; each item's content is plain text plus a bounding box. JSON renderer learns the new element types; Markdown renderer learns ordered-list emission.
3. `PdfStructParser` gains a per-page detection pass between running-furniture filtering and paragraph merging. The pass mutates the line stream by setting the claimed flag; it appends list aggregates to the page's content sequence.
4. The paragraph merger learns to skip claimed lines.

No public API surface beyond the new element types changes in Phase 1. The existing `Parse` / `AnalyzeTextLines` / `AnalyzeHeadingProbabilities` entry points are untouched.
