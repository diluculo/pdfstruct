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

### D-009 — lorem_ipsum paragraph segmentation: opposite failure modes

| field | value |
|---|---|
| fixture | `lorem_ipsum.pdf` (single page, 5 elements) |
| area | paragraph |
| ground truth | `# Lorem Ipsum` (heading); two introductory quoted paragraphs each starting with `"…"`; two body paragraphs (`Lorem ipsum dolor sit amet…` and `Suspendisse eu sapien…`). 5 elements total. Source: `src/PdfStruct.Tests/GroundTruth/lorem_ipsum.md`. |
| odl | Heading correct. Both quoted paragraphs and the first body paragraph are **merged into a single paragraph block** — 4 logical paragraphs become 2. Under-segmentation. |
| ours | Heading correct. The two quoted paragraphs are emitted as separate paragraphs (correct). The first body paragraph is **split into roughly seven fragments** — the words `gravida`, `placerat`, `Phasellus`, `vel`, `nibh`, `ipsum`, `nec`, `nunc` each become standalone paragraph blocks, separated by blank lines from the surrounding body text. The second body paragraph is correct. Over-segmentation. |
| judgment | **investigate**. ODL and PdfStruct fail in opposite directions on the same document — ODL under-segments, we over-segment. The pattern is the inverse of D-008 where ODL split a single document into too many headings and we collapsed three elements into one paragraph. Together D-008 and D-009 show that the paragraph merger needs work in both directions, and that ODL parity alone would not fix it because ODL's behaviour is also wrong vs ground truth. |
| followup | Phase 3+ paragraph-merger overhaul. The over-segmentation in `lorem_ipsum` is most likely caused by the merger treating in-line style changes (italics, weight, font name swaps applied to a few stylised words within a body paragraph) as paragraph-block boundaries. The under-segmentation in ODL on the same document and over-merging in our `minimal_document` output suggest the merger needs both stronger continuation signals (to bridge stylistic interruptions inside one paragraph) and stronger break signals (to separate genuinely-distinct paragraphs that share style). Ground-truth scoring once `compare-to-ground-truth.ps1` exists will give the precision/recall numbers needed to prioritise. |

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

### D-010 — kr_lorem_ipsum CJK line joining: shared failure at the line-join step

| field | value |
|---|---|
| fixture | `kr_lorem_ipsum.pdf` (single page, 4 elements) |
| area | paragraph |
| ground truth | `# 로렘 입숨` (heading, level 1); three Korean lorem-ipsum body paragraphs, each emitted as one Markdown line with no whitespace inserted at PDF visual line-wrap points — e.g. `더한 알리라.`, not `더한 알리 라.`. 4 elements total. Source: `src/PdfStruct.Tests/GroundTruth/kr_lorem_ipsum.md`. |
| odl | Heading correct, three paragraphs correctly bounded. The `content` string of each paragraph joins PDF visual lines with a single `' '` space — `더한 알리 라.` instead of ground-truth `더한 알리라.`. The Markdown surface is one long line per paragraph. |
| ours | Heading correct, three paragraphs correctly bounded. The `content` string of each paragraph keeps the PDF visual line breaks as literal `'\n'` — `더한 알리\n라.` instead of ground-truth `더한 알리라.`. The Markdown surface is visually broken across multiple lines per paragraph; CommonMark soft breaks render as a space at HTML / LLM consumption time, so the rendered effect matches ODL — both insert a space at a Korean mid-word wrap. |
| judgment | **investigate**. Paragraph segmentation matches ground truth on this fixture (a clean case unlike D-008 and D-009). The line-joining step fails the same way in both tools: neither tracks that wrapping inside Korean text occurs mid-word and therefore should not insert any separator at the join. The two outputs differ in surface form (literal `\n` vs space) but agree in rendered effect. With only one Korean ground-truth fixture available, one fixture is too few to commit to a strategy. |
| followup | Implementation strategy TBD once `kr_constitution` and `kr_patent` ground truth exist. The line-joining step needs to become script-aware in some form — Latin wrapping coincides with whitespace so a space separator is correct, while CJK wrapping does not — but the precise rule (mixed Latin–CJK boundaries, punctuation, hyphenation) is a design question that needs more ground-truth data than one fixture provides. ODL's space-insertion and our literal-`\n` are both candidates to re-evaluate; neither is privileged. Re-score both tools after each new Korean fixture lands. |

### D-011 — kr_constitution heading hierarchy: PdfStruct emits zero headings; ODL recovers most chapters but misclassifies articles

| field | value |
|---|---|
| fixture | `kr_constitution.pdf` (14 pages, ~155 expected heading elements) |
| area | heading |
| ground truth | Five-level hierarchy: H1 `대한민국헌법`; H2 `전문`, ten `제N장 …`, `부칙`; H3 articles `제N조` in chapters that don't subdivide further, plus `제1절 대통령` and `제2절 행정부` and the 부칙 articles; H4 `제66조`–`제85조` (under 제1절) and `제1관`–`제4관` (under 제2절); H5 `제86조`–`제100조` (within 관). 130 articles in monotonic sequence. Clauses (`①②③` markers) are body paragraphs, not list items. Source: `src/PdfStruct.Tests/GroundTruth/kr_constitution.md`. |
| odl | 30+ heading elements. H1 title `# 대한민국헌법` correct. Chapter labels classified as H2; many article entries classified as H3. The 전문 H2 is dropped (text merged into the preceding `[시행 …]` paragraph). Section / 관 sub-section levels and the deepest articles drop into **list items** with `제N조` on the bullet line, conflating the heading–list distinction; clauses end up as either list-item children or detached paragraphs depending on layout. Running headers and footers surface as 14 + 14 first-class elements; the document seal is extracted as an image. |
| ours | **Zero heading elements.** 236 elements total: 207 paragraphs + 25 nested list items + 3 lists. The H1 title is dropped entirely — the first emitted element is `전문` as a paragraph. Chapter labels (`제N장 …`, `부칙`) are paragraphs. Article labels are **merged inline with the first clause** so a single paragraph emits `제1조 ①대한민국은 민주공화국이다.`, losing both the article boundary and any heading level. Clauses 2 onwards (`②③…`) are correctly separate paragraphs. The CJK line-wrap artefact from D-010 is present throughout. |
| judgment | **investigate**. ODL outperforms PdfStruct by a wide margin on heading recovery for this fixture, and the gap is structural: PdfStruct's font-rarity classifier finds no rarity cluster strong enough to mark anything as a heading on the 14-page document, even though the title and chapter labels carry visibly distinct weight and size. The article-as-list mistake in ODL is likely a side-effect of the same wcag-algorithms list detection that drives D-001 — articles begin with a label-like prefix `제N조` followed by space and body, which matches the list-prefix shape. With ground truth on `us_constitution` and `plos_utilizing_llm` still missing, the scope of a fix cannot be decided here yet. What this fixture establishes is that font-rarity-only heading classification does not generalise to densely-structured Korean legal layouts. |
| followup | The heading classifier needs at least one signal beyond global font rarity for documents like this — a rarity model that operates within the document's own typographic distribution, an alternative classifier that runs as a strategy alongside the font-rarity one, or both. CLAUDE.md already calls out a `RegexHeadingClassifier` strategy slot composed via `CompositeElementClassifier`, distinct from `FontBasedElementClassifier`; future work should design how the two combine without one overriding the other on documents where they disagree. The dropped `대한민국헌법` title is the inverse of D-008's "sparse-document title miss" — the classifier appears to degenerate at both extremes (very few blocks, very many uniformly-styled blocks). The article-merged-into-first-clause failure is a separate paragraph-merger / line-grouping concern: `제N조 ①…` lays the article marker on the same line as body text, and the merger needs structural awareness to keep them apart. Re-score on `us_constitution` and a Western academic fixture before locking the design — a kr_constitution-only fix would likely regress `lorem_ipsum` or `kr_lorem_ipsum`. |

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
