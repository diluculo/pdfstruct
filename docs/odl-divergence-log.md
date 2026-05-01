# ODL divergence log

PdfStruct uses OpenDataLoader-pdf as a reference baseline for measurement. Each divergence is judged against (a) human-marked ground truth on our 5 fixtures, (b) PdfStruct's own goals (RAG context quality, idiomatic C# model), and (c) ODL's behavior — in that order. ODL is the most convenient baseline to diff against, but it is not the tiebreaker. When ground truth is unavailable for a divergence, mark it `investigate` until ground truth exists.

This file is the durable record of every observed divergence, the judgment we made, and what (if anything) we plan to do about it. It is updated alongside any work that changes how we compare to ODL — Phase reviews, fixture additions, ad-hoc investigations.

## How to use this log

For each divergence:

| Field | Meaning |
|---|---|
| **id** | Unique identifier so this entry can be cited from commits, issues, or other docs. Format `D-NNN`. |
| **fixture** | The PDF and the page (or page range) where the divergence was observed. |
| **area** | One of: bbox, reading order, list, heading, table, header/footer, image, captions, paragraphs, sanitization, dates, other. |
| **odl** | What ODL produces in the disputed region (one or two short sentences). |
| **ours** | What PdfStruct currently produces in the disputed region. |
| **judgment** | One of: `align-to-odl` (ground truth or our goals say ODL is right and we should change), `keep-divergence` (ground truth or our goals say we are right; ODL is the deviation), `align-to-odl-with-render-policy` (data model follows ODL faithfully, the rendering layer applies a deliberate lossy transform), `investigate` (no ground truth or measurement yet to support a decision). Includes a one-line reason. |
| **followup** | The work item this entry generates, or `none` if no action is planned. |

A divergence entry is closed when either (a) the work item lands and we re-check that the divergence resolves, or (b) the judgment changes and the entry is updated in place. Closed entries stay in the log; they are the record.

## Comparison generation

Use `run-odl-golden.ps1` (root) to populate `playground/<name>/odl/` with ODL's output (md, json, annotated PDF, page rasters) for every PDF in `playground/`. Use `run-pdfstruct.ps1` (root, with `-DebugLines`) to populate the parent folder with PdfStruct's output. Compare visually page by page or via JSON diff per fixture. New divergences land in this log as new `D-NNN` rows.

---

## Initial findings (2026-05-01, after Phase 2 commit `2098dc0`)

These are the first divergences observed after Phase 2 was committed. They were the input to the Phase 3 prioritisation discussion. Pages cited use the rendering produced by `run-pdfstruct.ps1 -DebugLines` and `run-odl-golden.ps1`.

### D-001 — list-item rendering format in Markdown

| field | value |
|---|---|
| fixture | every fixture with at least one list (e.g. `plos_utilizing_llm`, `kr_constitution`) |
| area | list |
| odl | Renders every list-item line as `- <body text>`, where the original label (`1.`, `[1]`, `(1)`, etc.) is **preserved verbatim inside the body text**. Ordered and unordered lists use the same `-` Markdown bullet. |
| ours | Renders ordered list items as `1. <body text>`, with the original label **stripped** from the body. Numbering style determines the prefix (`1.` vs `-`). The original label glyphs (`[1]`, `(1)`, `1)` etc.) are lost in Markdown output even though they are preserved in the JSON model under `Number` and the prefix/terminator pair. |
| judgment | **align-to-odl-with-render-policy**. The data-model loss is the problem; the Markdown surface can keep its `1.` style if needed. Decision: keep the JSON model authoritative (Number, prefix, terminator already preserved); change only the Markdown renderer to emit `- <original-line-text>` so the rendered citation matches the source document verbatim. Re-numbering on a partial detection then never appears in the rendered output. |
| followup | Renderer change: emit list items as `- <reconstructed line>` where the body includes the recovered label glyphs. Requires the detector to keep an unstripped `RawLineText` or to reconstruct it from `prefix + Number + terminator + ' ' + Body`. Phase 3 candidate. |

### D-002 — list count on `plos_utilizing_llm.pdf`

| field | value |
|---|---|
| fixture | `plos_utilizing_llm` (whole document) |
| area | list |
| odl | 19 list elements, 81 list items in JSON. References, methodology enumerations, and bullet-point body lists are all detected. |
| ours | 4 list elements after Phase 2 (was 0 before Phase 2). The methodology Arabic enumerations are detected; most references are not. |
| judgment | **investigate**. ODL detects more lists than we do, but ODL's count alone is not evidence of correctness — ODL's detector itself can over-detect (false positives) or mis-group, and we do not yet have ground-truth marks for this fixture to call which side is closer to the document's actual structure. The "we should match 19" framing was wrong; the right framing is "what does the page actually contain, and how many of those does each tool recover". Until ground truth exists for this fixture, this entry stays open as `investigate`. |
| followup | Once `playground/<fixture>/ground-truth.md` is marked by hand for `plos_utilizing_llm`, recompute precision and recall for both tools against ground truth, record the numbers here, and then judge. The Phase 3 scope decision should follow the ground-truth gap, not the gap to ODL's count. |

### D-003 — table detection completely absent

| field | value |
|---|---|
| fixture | `plos_utilizing_llm` (5 tables in ODL's output) and others |
| area | table |
| odl | 5 table elements with rows / cells / page numbers / cross-page linking metadata. |
| ours | Zero tables. The pipeline currently has no table phase. |
| judgment | **align-to-odl**, deferred. Table detection is a substantial undertaking (border detection, row clustering, cell content extraction) and is out of scope for the list-detection track. |
| followup | A separate Phase whose number is not yet decided — Phase 5+ candidate. Tracked here so the gap is not forgotten. |

### D-004 — images as first-class layout citizens vs. absent entirely

| field | value |
|---|---|
| fixture | `plos_utilizing_llm` (8 images in ODL output, files written under `odl/images/`), all fixtures with figures |
| area | image |
| odl | Images are **first-class content objects from the parsing stage onward**, not a post-extraction add-on. They appear in the per-page content sequence as their own objects with bounding boxes, alongside text lines and paragraphs, and they participate in reading-order placement, figure–caption pairing, decorative-image filtering, and tagged-PDF figure handling. File persistence is the *last* step before output: at that point, each image's bounding box is used to crop the rendered page and the crop is written under `<name>/images/imageFileN.<ext>`. The Markdown / JSON output references that file path. (Reference: the page-NNN.png files inside `playground/<name>/odl/` are **re-renderings of `<name>_annotated.pdf` produced by `run-odl-golden.ps1` for visual comparison**, not ODL's image extraction output. Actual extracted images live in the sibling `images/` directory.) |
| ours | Zero image elements at any stage. The pipeline never builds image objects, never participates them in reading order, and never writes crops. The `Models.ImageElement` type exists in the model but is never produced. |
| judgment | **align-to-odl**, deferred. The right model is "image is a layout citizen, with file persistence as the last step", not "extract images separately at the end". This affects how the future image phase must be designed — the extraction must hook into the line/paragraph stage, not bolted onto output. |
| followup | A future phase whose number is not yet decided (likely after Phase 3 list rescue and Phase 4 cross-page list joining). The phase needs three coordinated changes: (a) populate image objects during page parsing using PdfPig's image APIs, (b) include them in the reading-order and reconciliation passes alongside text blocks, (c) write a bbox-keyed crop at output time and reference the path from Markdown / JSON. Doing only (c) without (a) and (b) — i.e. extracting images as a post-process — would diverge from ODL's pipeline shape and is the wrong direction. |

### D-005 — header/footer kept as elements vs stripped (CLOSED 2026-05-01)

| field | value |
|---|---|
| fixture | `plos_utilizing_llm` (18 footers in ODL output across 18 pages); applies to every fixture with running headers/footers |
| area | header/footer |
| odl | Emits `footer` (and `header`) elements with their content as kids; the renderer can choose whether to include or hide them. Every glyph stays in the model. |
| ours | Strips repeating headers/footers entirely from the output unless `--include-running-headers` is set. |
| judgment | **align-to-odl-with-render-policy**. Decision: data model preserves headers and footers as first-class elements (ODL-faithful, every glyph accounted for); the default Markdown renderer suppresses them (RAG-clean output); the `--include-running-headers` flag, which already exists, toggles the renderer behaviour. JSON consumers always see the full structure; Markdown consumers see clean main content by default. |
| followup | Implementation: keep `Models.HeaderFooterElement` as the data-model carrier; change the running-furniture filter from "remove from `doc.Kids`" to "wrap as HeaderFooterElement and emit, then have the renderer suppress by default". Markdown renderer learns to skip header/footer elements unless `--include-running-headers` was set. The CLI flag wiring stays as it is. Tracked as a follow-up implementation phase whose number is not yet decided; not gating Phase 3. |

### D-006 — heading level scheme (CLOSED 2026-05-01)

| field | value |
|---|---|
| fixture | `plos_utilizing_llm` (33 headings in ODL with levels mixing `Doctitle`, `Subtitle`, and numeric `1`..`8`) |
| area | heading |
| odl | Mixed-vocabulary scheme: `"level": "Doctitle"`, `"level": "Subtitle"`, and `"level": "<number>"`. JSON exposes the value uncapped; the document-tree depth determines the integer. |
| ours | Uses `"level": "Title" | "Section" | "Subsection" | "Level <n>"` (string label) and `"heading level": <int>` capped at 6 to match Markdown's H1..H6. |
| judgment | **align-to-odl-with-render-policy**. Decision: JSON emits `"heading level": <int>` 1..N with no cap; an optional `"level name"` field carries an ODL-parity human-readable name (`"Doctitle"`, `"Subtitle"`, or the integer as a string). The Markdown renderer clamps to H6 by `Math.Clamp(level, 1, 6)` at render time only. Rationale: the data model never loses information (depth `7` still says `7`); the rendering convention stays Markdown-compatible. Our existing `"level"` string ("Title"/"Section"/"Subsection") may either remain alongside `"level name"` for backward compat, or be replaced by `"level name"` outright; the choice is small and is recorded with the implementation, not here. |
| followup | Implementation: add an uncapped numeric heading level to the model; map the existing English vocabulary to ODL-style names when emitting JSON; change Markdown rendering to `Math.Clamp(level, 1, 6)`. Update `AssignHeadingLevels` to produce an uncapped integer instead of clamping at level assignment time. Tracked as a follow-up implementation phase; not gating Phase 3. |

### D-008 — minimal_document classification: both tools fail, in different ways

| field | value |
|---|---|
| fixture | `minimal_document.pdf` (single page, 3 elements) |
| area | heading + paragraph + caption |
| ground truth | `# The Crow and the Pitcher` (heading, level 1); body paragraph; `*Little by little does the trick.*` (italicised moral, treated as emphasis on a paragraph). Source: `src/PdfStruct.Tests/GroundTruth/minimal_document.md`. |
| odl | Title classified as `## ...` (heading, level **2** instead of 1). Body paragraph correct. Moral classified as `# Littlebylittledoesthetrick.` — promoted to a level-1 heading **and** the word spacing is lost (a separate ODL bug at the glyph-clustering stage). |
| ours | Title classified as paragraph (heading miss). Body paragraph correct in content. The three logical elements are merged into a single paragraph block in the rendered Markdown — no blank-line separation between title, body, and moral, indicating that the page-level paragraph merger is over-aggressive on a sparse document. |
| judgment | **investigate** (per-fixture entry). Both tools fail vs ground truth, and they fail in mutually inconsistent ways: ODL over-promotes (heading false positive) while we under-detect (heading false negative); ODL preserves paragraph separation while we collapse them. This is the first concrete case where ODL's behaviour cannot serve as a ceiling — aligning to ODL would still leave us wrong. |
| followup | Two work items, both Phase 3+ candidates: (a) heading classifier needs to recover at least the document title in sparse fixtures; the current font-rarity model degenerates when there are very few blocks. (b) The Markdown renderer or paragraph merger must preserve separation between conceptually-distinct paragraph elements; the current `AppendLine`-after-element pattern is correct in principle but the elements themselves are being merged before they reach the renderer. The ground-truth file makes both gaps measurable as recall (heading: 1/1 expected vs 0/1 ours, 1/1 ours-as-heading vs 0/1 ours; paragraph: 2 expected vs 1 ours). |

### D-007 — list-item bbox vs ODL

| field | value |
|---|---|
| fixture | `plos_utilizing_llm` page 18 (references list) |
| area | bbox |
| odl | (to be measured by reading the json bounding boxes) |
| ours | A single list element bounding box covers all confirmed reference items including absorbed children; individual list items have their own bounding boxes inside it. |
| judgment | **pending**. Need a per-item bbox comparison before judging. |
| followup | Pull both JSONs through a per-fixture diff utility (not yet written) that pairs list elements by content and reports bbox deltas. |

---

## Format specification for new entries

When adding a new entry, use this template:

```markdown
### D-NNN — short title

| field | value |
|---|---|
| fixture | `<name>.pdf` (page or page range) |
| area | bbox / reading order / list / heading / table / header-footer / image / caption / paragraph / sanitization / date / other |
| odl | one or two sentences describing ODL's output in the disputed region |
| ours | one or two sentences describing PdfStruct's output |
| judgment | align-to-odl / align-to-odl-with-render-policy / keep-divergence / investigate — one-line reason |
| followup | the work item this generates, or `none` |
```

Increment the `D-NNN` counter and append. Do not delete or renumber existing entries.
