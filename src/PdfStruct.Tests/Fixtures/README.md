# Test Fixtures

PDF documents used by the PdfStruct test suite to verify extraction quality
across realistic layouts. Fixtures fall into two categories: external
documents with explicitly permissive copyright status, and synthetic
documents authored specifically for this project.

## Current fixtures

| File | Pages | Layout | Purpose |
|------|-------|--------|---------|
| `kr_constitution.pdf` | 14 | Single column, Korean | Reading order, heading hierarchy, running header/footer detection |
| `us_constitution.pdf` | 18 | Double columns, English | Article/Amendment hierarchy, Latin-script reading order |
| `plos_game_based_education.pdf` | 14 | Asymmetric two-column, English | Academic journal layout, sidebar metadata, page footers |
| `plos_utilizing_llm.pdf` | 18 | Asymmetric two-column, English | Bordered tables, figures with captions, inline citations, references list |
| `magazine_article.pdf` | 2 | Magazine, English | Floating images, pull-quotes, page badges, byline |
| `table_of_contents.pdf` | 2 | Magazine TOC, English | **(v0.2+)** Multi-column TOC, rotated furniture, repeating placeholder text |
| `kr_lorem_ipsum.pdf` | 1 | Single column, Korean | Korean prose baseline, quoted dialogue |
| `letter.pdf` | 1 | Single column, English | Letter format (salutation, body, sign-off), stylized header |
| `lorem_ipsum.pdf` | 1 | Single column, Latin/English | Italic block quotes, title + epigraph + body |
| `minimal_document.pdf` | 1 | Single column, English | Title + paragraph + epilogue, repeated phrase pattern (smoke test) |

## External fixtures

Documents authored by third parties and redistributed here under licenses
that permit it. Each entry records the source, the legal basis for
inclusion, and the extraction behaviors the fixture is intended to exercise.

### `kr_constitution.pdf`

The Constitution of the Republic of Korea, as published by the Korea
Ministry of Government Legislation
([국가법령정보센터](https://www.law.go.kr/)).

Excluded from copyright protection by **Article 7, Item 1 of the Korean
Copyright Act**, which expressly removes constitutions, statutes, treaties,
ordinances, and rules from the scope of protected works. The PDF itself is
a public-sector work and may be redistributed without restriction.

This fixture exercises:

- Article numbering (`제1조` through `제130조`) provides a strict
  monotonic sequence that automatically validates reading order.
- Section markers (`제N장`, `제N절`, `제N관`) form a clean four-level
  heading hierarchy with explicit indentation cues.
- Running headers (`대한민국헌법`) and footers
  (`법제처 N 국가법령정보센터`) repeat across all 14 pages, exercising
  the page-furniture detector.
- Justified single-column body text produces short orphan tail lines
  (e.g. `한다.`) that exercise the paragraph-merge logic.

### `us_constitution.pdf`

The Constitution of the United States, comprising the original 1787 text
and all 27 ratified amendments. Cover page removed; body content only.

The constitutional text is in the public domain on two independent grounds:
it was drafted in 1787 (well past any conceivable copyright term), and U.S.
federal government works are excluded from copyright protection by
**17 U.S.C. § 105**.

Originally distributed by the National Constitution Center
([constitutioncenter.org](https://constitutioncenter.org/)).

This fixture exercises:

- Double-column reading order — the principal value-add over the Korean
  constitution fixture, which is single-column.
- Roman numeral article hierarchy (`Article. I.` through `Article. VII.`)
  as a counterpart to Korean `제N장` patterns.
- Inconsistent section markers (`SECTION. 1`, `SECTION. 5.`, `SECTION 1`
  appear across different parts of the document) — useful for testing
  pattern-matching robustness.
- Bracketed amended text with footnote markers (`[ ... ]*`) as inline
  annotations.
- Multi-column signatory list grouped by state (Article VII closing).

### `plos_game_based_education.pdf`

Erdem E, Düzgün G (2026). "The effect of game-based education on adherence
to treatment and anxiety level in type 2 diabetics started on insulin
therapy." *PLoS ONE* 21(3): e0345292.
[doi.org/10.1371/journal.pone.0345292](https://doi.org/10.1371/journal.pone.0345292)

Licensed under **Creative Commons Attribution 4.0 (CC-BY)**, which permits
redistribution with attribution.

This fixture exercises:

- Asymmetric two-column layout — narrow metadata sidebar (Citation, Editor,
  Received, Copyright) on the left, full-width body on the right. Tests
  whether XY-Cut produces correct reading order across visually distinct
  columns of unequal width.
- Heading hierarchy with four levels: kicker (RESEARCH ARTICLE), title,
  Abstract, sub-section labels (Background, Objective, Methods, Results).
- Repeating page footer (`PLOS One | <doi> <date>   <pageNum> / 14`) on
  every page — exercises the page-furniture detector with a different
  pattern than the Korean constitution.
- Non-ASCII Latin characters (`Düzgün`, `Tınaztepe`, `Türkiye`) as a
  Latin-extended counterpart to Hangul handling.
- Inline ORCID glyphs and superscript affiliation markers — exercises
  letter-level extraction noise.
- Standard academic body sections (Methods, Results, Discussion,
  References) across 14 pages.

### `plos_utilizing_llm.pdf`

Teich MC, Escobari B, Rehbein M (2026). "Utilizing large language models
to construct a dataset of Württemberg's 19th-century fauna from historical
records." *PLoS ONE* 21(3): e0344181.
[doi.org/10.1371/journal.pone.0344181](https://doi.org/10.1371/journal.pone.0344181)

Licensed under **Creative Commons Attribution 4.0 (CC-BY)**, which permits
redistribution with attribution.

Shares the standard PLOS layout template with `plos_game_based_education.pdf`,
allowing cross-fixture regression checks: any PLOS-specific extraction
behavior should be consistent between the two. This fixture is the table
and figure counterpart, while `plos_game_based_education.pdf` covers the
text-heavy academic baseline.

This fixture exercises:

- Bordered tables (Tables 1–5) — verifies that table regions are detected
  and that text inside cells does not leak into surrounding paragraphs.
  Cell-level extraction is out of scope for v0.1; the assertion is region
  isolation only.
- Figures with captions (Figs 1–6) — a mix of raster image (`Fig 1`,
  district map), vector workflow diagram (`Fig 2`), distribution plot
  (`Fig 3`), and choropleth maps (`Figs 4–6`). Captions begin with `Fig N.`
  and should be classified independently of the figure content via
  spatial adjacency rather than caption-prefix patterns.
- Inline citation markers (`[1–7]`, `[8]`, `[10]`) embedded in body
  paragraphs — must not break paragraph flow or be classified as separate
  list items.
- Numbered references list (56 entries on pages 16–18) in `1. Author...`
  format — exercises the boundary between paragraph and list-item
  classification on a long uniform sequence.
- Mid-paragraph quoted German text and Latin scientific names (italicized)
  interspersed with English body text — a different kind of mixed-script
  noise than the affiliation-line pattern in `plos_game_based_education`.

## Synthetic fixtures

Documents authored specifically for the PdfStruct test suite. No
third-party copyright applies; these PDFs are part of the project source
tree and are governed by the project's Apache-2.0 license.

### `magazine_article.pdf`

A short two-page magazine spread authored for layout-stress testing.
Combines a stylized title, body copy overlaid on photographic background,
floating image regions, a large pull-quote, byline attribution, and
page-number badges.

This fixture exercises:

- Non-rectangular content regions — body text wraps around image areas
  rather than confining itself to a clean column. Stresses the
  assumptions XY-Cut makes about rectangular reading regions.
- Pull-quote detection — `"Insert a quote from the article"` appears in
  display-size font on page 2 and should not be classified as a heading
  even though its font is larger than body text.
- Page furniture distinct from running headers — `Page 1`, `Page 2`
  badges appear in the bottom-right corner with stylized typography.
- Byline placement — `By Reporter name` appears at end of article on
  page 2 in italic, exercising end-of-content metadata detection.
- Variable column widths within a single page (body column shifts width
  as image regions intrude).

### `table_of_contents.pdf`

A two-page magazine table-of-contents authored for stretch-goal layout
testing. Multiple visual columns, large display-size page numbers
interleaved with body entries, embedded photographs, and rotated text in
the page margins.

**This fixture is registered with `[Fact(Skip = "...")]` in the test
suite — full extraction is a v0.2+ goal.** It is included now so the
fixture is in place when rotated-text and image-region detection land.

This fixture exercises (planned, currently expected to fail):

- Rotated furniture text — `WWW.STRUCTUREDMAG.SITE.COM` runs vertically
  in the left margin. Requires the rotated-text detector planned for v0.2,
  the same capability needed for arXiv-style stamps.
- Six visual columns per page, mixing entry text + page number sub-columns
  with display-size page-number boxes (`31`, `41`, `100`, `103`, `108`,
  `111`). XY-Cut without column-grouping post-processing is expected to
  fragment these.
- Embedded photographs that participate in reading flow rather than sitting
  in a separate region — image-region detection is required to assemble a
  coherent reading order.
- Highly repetitive placeholder text (`The quick brown fox jumped` appears
  ~50 times across two pages) — exercises a failure mode of the
  running-header/footer detector. Repetition alone must not classify these
  as page furniture; spatial clustering and Y-band consistency must
  override raw repetition count.

### `kr_lorem_ipsum.pdf`

A single page of Korean prose generated from public-vocabulary
lorem-ipsum generators ([hanipsum.com](https://hanipsum.com/)).
Produces syntactically Korean-looking but semantically empty text.

This fixture exercises:

- Korean prose baseline outside the legal-document domain — paired with
  `kr_constitution.pdf` it tests whether the parser is biased toward the
  legal layout patterns it was developed against.
- Korean quotation marks (`"…"`) interspersed with narrative —
  exercises the line-continuation logic when sentences end inside
  quoted dialogue.
- Mixed sentence terminators including `다.`, `라.`, `라` (no period),
  `요.`, `?` — exercises the paragraph-merge heuristic across the full
  range of Korean sentence-final endings.

### `letter.pdf`

A short personal letter authored for testing letter-format documents.
Distinct salutation block, italic-styled body, and sign-off block.

This fixture exercises:

- Letter-specific structural roles — `MY DEAREST` (display header) and
  `Maria,` (salutation) are visually distinct from the article-style
  headings the parser is normally tuned for.
- Italic-styled body that is *not* a quote — distinguishes between
  italic-as-emphasis and italic-as-quote.
- Sign-off block (`Yours always, Felix`) as a distinct trailing element
  rather than a continuation of the body paragraph.

### `lorem_ipsum.pdf`

The canonical placeholder text in Latin, with a leading Latin epigraph
and its English translation. Single column, single page.

This fixture exercises:

- Block quote detection — two italic epigraphs appear before the body
  text. Should be distinguishable from body paragraphs by font style
  alone.
- Title + epigraph + body structure — a common pattern in essays,
  magazine columns, and book chapters.
- Latin-script baseline with no domain-specific features, useful as a
  smoke-test fixture: if extraction breaks here, something fundamental
  has regressed.

### `minimal_document.pdf`

"The Crow and the Pitcher" from Aesop's Fables, in the standard English
translation by Joseph Jacobs (1894). The original Greek fable predates
copyright by ~2,500 years; the English translation is in the public
domain in all major jurisdictions (US: pre-1929 publication).

Despite the file's name, the fixture exercises slightly more than a
single paragraph: a short title, one body paragraph, and a one-line
moral on its own. This is the project's minimal smoke-test fixture —
if extraction breaks here, something fundamental has regressed.

This fixture exercises:

- Trailing standalone line — `Little by little does the trick.` should
  remain a separate element rather than being merged into the body
  paragraph as an orphan tail.
- Within-paragraph repetition — three near-identical sentences
  (`Then he took ... pebble and dropped it into the pitcher.`)
  appearing consecutively. The running-header detector must not
  classify these as page furniture even if a similar repetition
  occurs across pages in another fixture, because the repetitions
  are local to a single paragraph.
- Title classification on a one-page document — `The Crow and the
  Pitcher` should be H1 even when there is no other heading on the
  page to compare against.
  
## Adding new fixtures

When adding a fixture, prefer sources in this order:

1. **Public-domain government documents** — laws, regulations, court
   decisions, agency reports. Always check the issuing jurisdiction's
   copyright statute; many countries explicitly exempt government works
   (Korea, Article 7 of the Copyright Act; United States,
   17 U.S.C. § 105).
2. **Permissively licensed corpora** — documents under CC-BY, CC0, or
   Apache-2.0. PLOS journals, eLife, MDPI, and the PubMed Central
   Open Access Subset are reliable sources of CC-BY academic PDFs.
3. **Synthetic documents authored for the project** — when testing a
   specific edge case in isolation, generate a minimal PDF rather than
   borrowing one. Authored fixtures fall under the project's own
   license and require no third-party attribution.

Avoid:

- Scanned books, magazines, or news articles, even for "small excerpts."
- Academic papers from preprint servers without checking the per-paper
  license — arXiv submissions vary, and the default arXiv license does
  not permit redistribution.
- Marketing materials, product manuals, or web-scraped content.
- Documents fetched from Sci-Hub, ResearchGate, or Academia.edu — even
  when the original work is openly licensed, those sites' hosted copies
  may be uploaded without authorization.

Filename convention: prefix with the document's origin (`kr_`, `us_`,
`plos_`) when the fixture represents a specific national or publisher
template; use a plain descriptive name (`magazine_article`,
`lorem_ipsum`) when the fixture is generic or synthetic.

Each fixture entry should record source URL, copyright status, and which
extraction behaviors it is intended to exercise. If a fixture only tests
one narrow capability, name it accordingly (e.g. `multi_column_2up.pdf`,
`table_with_merged_cells.pdf`) rather than borrowing real documents
that test many things at once.

## Notes for contributors

- Fixtures are loaded via `Path.Combine(AppContext.BaseDirectory,
  "Fixtures", "<filename>.pdf")`. New `*.pdf` files in this folder
  are picked up automatically by the `Content Include="Fixtures\*.pdf"`
  glob in `PdfStruct.Tests.csproj`.
- Keep individual fixtures under 5 MB. Larger documents should live
  outside the repository and be downloaded by CI on demand.
- Fixtures that are not yet expected to extract correctly should still
  be committed, but their tests must be registered with
  `[Fact(Skip = "...")]` referencing the issue or roadmap milestone
  that tracks the missing capability. `table_of_contents.pdf` is the
  current example — it is in the repository so future regression
  tests have ground truth, but its assertions do not run on v0.1.
- Synthetic fixtures should be small and purpose-specific. If a single
  fixture starts exercising more than one extraction concern, consider
  splitting it.

