# Fixture baselines

Frozen Markdown and JSON outputs of the `pdfstruct extract` CLI for a representative subset of committed fixtures. They are not used for assertions today; they exist so that the diff produced by a future change is reviewable as a single `git diff` artefact.

The baselines were captured against the pipeline state immediately preceding Phase 1 list detection (see `docs/list-detection-spec.md` and `docs/adr/0001-list-detection-license.md`). They are the reference point for the Phase 1 acceptance review.

## Coverage

| Fixture | Why it is here |
| --- | --- |
| `kr_constitution` | Korean legal text with running headers/footers and `제N조` heading patterns. Phase 1 (Arabic-only) is expected to leave this output unchanged. |
| `us_constitution` | Western legal text with Article / Amendment heading hierarchy. Phase 1 is expected to leave most of this unchanged; a Section may incidentally pick up a small numeric list. |
| `lorem_ipsum` | Single-column body without any list semantics. Phase 1 must produce a zero-byte diff against this baseline. |
| `plos_utilizing_llm` | Two-column academic article, the densest Phase 1 target. Numbered enumerations in body and references are expected to convert from paragraphs into list elements. |
| `table_of_contents` | Magazine layout with page-number columns. Phase 1 should not invent new lists out of the page-number column; the baseline guards against that regression. |

## How to regenerate

```
dotnet build PdfStruct.sln -c Release --nologo
for f in kr_constitution us_constitution lorem_ipsum plos_utilizing_llm table_of_contents; do
  dotnet run --project src/PdfStruct.Cli --no-build -c Release -- \
    extract "src/PdfStruct.Tests/Fixtures/${f}.pdf" \
    -o "src/PdfStruct.Tests/Baselines/${f}.md"
  dotnet run --project src/PdfStruct.Cli --no-build -c Release -- \
    extract "src/PdfStruct.Tests/Fixtures/${f}.pdf" \
    -o "src/PdfStruct.Tests/Baselines/${f}.json" --format json
done
```

Regeneration is intentional during a phase boundary. Within a phase, edits to baselines must be reviewed alongside the code change that produced them.
