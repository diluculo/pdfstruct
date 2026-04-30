# CLAUDE.md

Guidance for Claude Code agents working in this repository.

## What this repo is

PdfStruct is a .NET library and CLI for **RAG-optimized structured extraction** from PDFs. It produces Markdown (for LLM context) and OpenDataLoader-compatible JSON (for citations and bbox-grounded retrieval) from a single parse.

The C# / .NET ecosystem currently has no equivalent — PdfStruct is the gap-filler. Inspiration comes from [OpenDataLoader-pdf](https://github.com/datactivist/opendataloader-pdf), and ODL JSON shape compatibility is a deliberate design goal, not just a coincidence.

## Layout

```
src/
  PdfStruct/         Library (NuGet target: PdfStruct)
    Models/          Document model: ContentElement, BoundingBox, etc.
    Analysis/        Layout (XyCutLayoutAnalyzer) + classifier (FontBasedElementClassifier)
    Rendering/       Markdown + JSON renderers (ODL-compatible JSON keys)
    Safety/          Prompt-injection filter, text sanitizer
    PdfStructParser  Public entry point
  PdfStruct.Cli/     Console app, packaged later as `dotnet tool` (command name `pdfstruct`)
  PdfStruct.Tests/   xUnit
tests/fixtures/      PDFs that ship with the repo (must be copyright-clean)
playground/          Gitignored sandbox for local user PDFs (see playground/README.md)
todo/                Local planning notes (gitignored)
```

The CLI is a thin shell over the library — keep it that way. Anything reusable goes into the library; the CLI handles arg parsing, IO, and debug-image rendering only.

## Build, test, format

```bash
dotnet restore PdfStruct.sln
dotnet build PdfStruct.sln -c Release --no-restore
dotnet test PdfStruct.sln -c Release --no-build
dotnet format PdfStruct.sln --verify-no-changes --no-restore
```

Target framework: `net8.0` (pinned via `global.json`). C# language version is `12.0` (pinned via `Directory.Build.props`). Package versions are centrally managed in `Directory.Packages.props`.

## Running the CLI locally

```bash
dotnet run --project src/PdfStruct.Cli -- extract playground/some.pdf
dotnet run --project src/PdfStruct.Cli -- extract playground/some.pdf -o out.md
dotnet run --project src/PdfStruct.Cli -- extract playground/some.pdf --debug-image out/debug
```

Drop test PDFs into `playground/` (gitignored). The build also copies the apphost to `pdfstruct.exe` next to `PdfStruct.Cli.exe` so you can invoke `bin/.../pdfstruct.exe` directly.

## Conventions

- **XML doc comments**: every public type and method gets a full `<summary>`/`<param>`/`<returns>`/`<exception>` block. Private methods get at least a one-line `<summary>`. Keep the style concise — match what's already in `PdfStructParser.cs` and `Renderers.cs`.
- **No comments that explain *what* the code does** — well-named identifiers do that. Comments only for non-obvious *why*: a hidden constraint, a workaround, an invariant a reader would otherwise miss.
- **No backwards-compatibility shims, removed-code markers, or "// TODO future" placeholders.** If something is unused, delete it.
- **ODL JSON compatibility is load-bearing.** The JSON renderer uses ODL key names with spaces (`"file name"`, `"bounding box"`, `"page number"`, etc.) and ODL element typing (`level: "Title"` plus `heading level: 1`). Don't unilaterally rename keys, change `bounding box` from array to object, or remove duplicated fields — those are ODL-mandated, not redundant. If you genuinely need a non-ODL JSON shape, add it as an option, don't break the default.
- **Date/time fields** in JSON should serialize as ISO 8601 (`2026-04-30T11:30:09+09:00`), not the PDF raw `D:YYYYMMDDhhmmss±HH'mm'` form.
- **Heading classification**: language-specific heuristics (e.g., Korean legal patterns like `제N장`, `전문`, `부칙`) belong in their own `IElementClassifier` strategy, not inlined into `FontBasedElementClassifier`. Compose classifiers; don't stack heuristics into one class.
- **CLI executable naming**: do not set `<AssemblyName>pdfstruct</AssemblyName>` on `PdfStruct.Cli.csproj` — it case-insensitively collides with the library's `PdfStruct.dll` and breaks `GenerateDepsFile`. The post-build `Copy` target in the csproj produces `pdfstruct.exe` alongside the original output. The eventual `dotnet tool` packaging will use `<ToolCommandName>pdfstruct</ToolCommandName>` for the launcher.

## Fixtures

`tests/fixtures/` holds copyright-clean PDFs that ship with the repo. The primary regression fixture is `대한민국헌법.pdf` (Constitution of Korea, public domain under Article 7 of the Korean Copyright Act). It is uniquely useful for layout-analysis testing because:

- Article numbers `제1조` ... `제130조` form a strict monotonic sequence — perfect for reading-order regression assertions.
- Section markers (`제N편`, `제N장`, `제N절`, `제N관`) form a clean four-level heading hierarchy.
- Running headers (`대한민국헌법`) and footers (`법제처 N 국가법령정보센터`) repeat across all 14 pages — exercise the running-header detector.
- Justified single-column body produces orphan tail lines (e.g. `한다.`) that exercise paragraph-merge logic.

When adding a new fixture, follow the source/license rules in `tests/fixtures/README.md`. Never commit copyrighted scans, marketing PDFs, or web-scraped content.

For PDFs that are private, large, or uncertain copyright-wise, drop them into `playground/` instead — that directory is gitignored.

## Status

PdfStruct is **early alpha**. The public API and JSON output schema may still change before the first stable release. Be willing to break things now — schema break cost rises sharply after v0.1 alpha public.

## What "done" means here

- The change builds clean (`dotnet build` with no warnings on Release).
- Tests pass (`dotnet test`).
- Format check passes (`dotnet format --verify-no-changes`).
- For library changes that affect output: run the CLI against `tests/fixtures/대한민국헌법.pdf` and eyeball the Markdown / JSON output. Layout regressions are not always caught by unit tests yet.
- XML doc comments are present on every new public/internal API, with `<summary>` at minimum on private members.
