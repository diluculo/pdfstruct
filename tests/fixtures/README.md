# Test Fixtures

PDF documents used by the PdfStruct test suite to verify extraction quality
across realistic layouts. All fixtures are either public-domain government
publications or content created specifically for testing — no third-party
copyrighted material lives in this folder.

## Current fixtures

| File | Pages | Layout | Purpose |
|------|-------|--------|---------|
| `대한민국헌법.pdf` | 14 | Single column, Korean | Reading order, heading hierarchy, running header/footer detection |
| `us_constitution.pdf` | 18 | Double columns, English | Article/Amendment hierarchy, Latin-script reading order |

## Sources and copyright

### `대한민국헌법.pdf`

The Constitution of the Republic of Korea, as published by the
Korea Ministry of Government Legislation
([국가법령정보센터](https://www.law.go.kr/)).

Excluded from copyright protection by **Article 7, Item 1 of the Korean
Copyright Act**, which expressly removes constitutions, statutes, treaties,
ordinances, and rules from the scope of protected works. The PDF itself is
a public-sector work and may be redistributed without restriction.

This fixture is well-suited to layout-analysis testing because:

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

The Constitution of the United States, comprising the original 1787
text and all 27 ratified amendments. Cover page removed; body content
only.

The constitutional text is in the public domain on two independent
grounds: it was drafted in 1787 (well past any conceivable copyright
term), and U.S. federal government works are excluded from copyright
protection by **17 U.S.C. § 105**.

Originally distributed by the National Constitution Center
([constitutioncenter.org](https://constitutioncenter.org/)).

This fixture exercises:

- Double-column reading order — the principal value-add over the
  Korean constitution fixture, which is single-column.
- Roman numeral article hierarchy (`Article. I.` through `Article. VII.`)
  as a counterpart to Korean `제N장` patterns.
- Inconsistent section markers (`SECTION. 1`, `SECTION. 5.`, `SECTION 1`
  appear across different parts of the document) — useful for testing
  pattern-matching robustness.
- Bracketed amended text with footnote markers (`[ ... ]*`) as inline
  annotations.
- Multi-column signatory list grouped by state (Article VII closing).

## Adding new fixtures

When adding a fixture, prefer sources in this order:

1. **Public-domain government documents** — laws, regulations, court
   decisions, agency reports. Always check the issuing jurisdiction's
   copyright statute; many countries explicitly exempt government works.
2. **Permissively licensed corpora** — documents from Apache-2.0,
   MIT, or CC-BY repositories. Record the source URL and license in
   the table above.
3. **Synthetic content** — PDFs you generate yourself from
   non-copyrighted text (Lorem Ipsum variants, your own writing).
   Prefer this when testing a specific edge case in isolation.

Avoid:

- Scanned books, magazines, or news articles, even for "small excerpts."
- Academic papers unless they are under an open license (arXiv submissions
  vary; check each paper's license tag).
- Marketing materials, product manuals, or web-scraped content.

Each fixture entry should record source URL, copyright status, and which
extraction behaviors it is intended to exercise. If a fixture only tests
one narrow capability, name it accordingly (e.g. `multi_column_2up.pdf`,
`table_with_merged_cells.pdf`) rather than borrowing real documents that
test many things at once.

## Notes for contributors

- Fixtures are loaded via `Path.Combine(AppContext.BaseDirectory,
  "fixtures", "<filename>.pdf")`. Update `PdfStruct.Tests.csproj` to
  include new files with `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`.
- Keep individual fixtures under 5 MB. Larger documents should live
  outside the repository and be downloaded by CI on demand.
- Do not commit fixtures that fail to parse with the current code —
  add an `[Fact(Skip = "...")]` test instead, with a TODO referencing
  the issue that tracks the fix.

