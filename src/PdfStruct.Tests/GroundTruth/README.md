# Fixture ground-truth files

Hand-marked Markdown for committed test fixtures. Each `<name>.md` here is the **expected** output for `<name>.pdf` under `src/PdfStruct.Tests/Fixtures/`, written by a human reading the PDF page by page. Ground truth is the tiebreaker when PdfStruct's output and ODL's reference output disagree (see `docs/odl-divergence-log.md`).

These files exist to support precision/recall measurement of element detection. Without ground truth, every divergence vs ODL stays `investigate` in the log because we cannot say which side is correct. With ground truth, we can score each tool independently and rank work items by recovery target instead of by gap to ODL's count.

## Format

Plain GitHub-flavored Markdown. The Markdown syntax itself encodes the element type:

| Markdown | Element type |
|---|---|
| `# Title` | heading, level 1 |
| `## Subtitle` | heading, level 2, etc. through `######` (level 6) |
| `paragraph text` | paragraph (separated from neighbours by a blank line) |
| `- item` | unordered list item |
| `1. item` | ordered list item (numbering preserved as written) |
| `*emphasis*` or `**bold**` | inline emphasis inside a paragraph; **does not change element type** |
| `> blockquote` | caption or pull-quote — when the source document treats the quoted text as structurally distinct from the surrounding body |
| `\| col1 \| col2 \|` table syntax | table |
| ``` ` ` ``` code spans / fences | code (inline or block) |

Conventions for cases the basic Markdown spec is ambiguous about:

- **Caption**. If the source PDF presents a line as a caption (figure caption, photo credit, italicised moral, attribution under a quote), mark it as a Markdown blockquote `> *text*`. The blockquote signals "structurally distinct from body"; the italics inside hint at the typographic emphasis. Plain `*text*` between paragraphs stays a paragraph that happens to be italicised — not a caption.
- **Heading-like enumerations**. A line like `Article I` followed by `Section 1.` is a heading hierarchy in the legal corpus, not an ordered list. Mark them as `# Article I` and `## Section 1.`, not as `1. Article I`.
- **Header / footer**. Do not mark running page headers, page numbers, or running footers in the ground truth. They are not part of the document's main content stream and the default Markdown renderer suppresses them anyway.
- **Image and table presence**. Note an image with `![alt](image)` and a table with the standard pipe table form. Bounding boxes are not represented; this format measures element-type-and-content recall, not bbox accuracy.

When in doubt, the human marker should mark what they would *want* the extractor to produce for downstream RAG consumption. The ground truth represents the goal, not what any particular tool happens to do today.

## Coverage

Six fixtures cover PdfStruct's regression matrix. They are added one at a time as time permits.

| fixture | added | notes |
|---|---|---|
| `minimal_document` | 2026-05-02 | single-page parable; used to bootstrap the format |
| `lorem_ipsum` | 2026-05-02 | single-column body with quoted introductory paragraphs; tests paragraph segmentation |
| `kr_lorem_ipsum` | 2026-05-02 | Korean lorem-ipsum body; tests CJK line-joining inside a paragraph (no separator at mid-word wrap points) |
| `kr_constitution` | 2026-05-02 | Korean legal text with `제N조` headings and `①②③` clauses; tests deep heading hierarchy (5 levels) and ordered lists inside articles |
| `us_constitution` | — | Western legal text with Article / Amendment hierarchy |
| `plos_utilizing_llm` | — | two-column academic with references and bullet lists |

When all six exist, `compare-to-ground-truth.ps1` (not yet written) will compute element-level precision and recall for both PdfStruct and ODL against this set.
