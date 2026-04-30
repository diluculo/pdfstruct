# PdfStruct

**PDF layout intelligence for .NET** — structured extraction with bounding boxes, reading order, and semantic element detection.

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

## Why PdfStruct?

The .NET ecosystem has **zero** RAG-optimized PDF extraction libraries. Python has OpenDataLoader, Docling, pymupdf4llm, Marker — C# has nothing.

PdfStruct fills that gap: a pure .NET library that extracts **structured content** from PDFs — headings, paragraphs, tables, lists — with bounding boxes for every element. Output as Markdown (for LLM context) or JSON (for citations). No GPU, no cloud, no JVM.

## Status

PdfStruct is an early alpha. The current implementation focuses on text layout, reading order, heading detection, Markdown rendering, JSON rendering, and safety filtering. The public API and output schema may still change before the first stable release.

The library currently targets:

- `net8.0`

The development SDK is pinned by [`global.json`](global.json). `net8.0` is the baseline for new consumers; older target frameworks will be considered only if there is a concrete compatibility need.

## Quick Start

Once the package is published:

```bash
dotnet add package PdfStruct --prerelease
```

```csharp
using PdfStruct;

var parser = new PdfStructParser(new PdfStructOptions
{
    Format = OutputFormat.Both,
    SanitizeText = true
});

var result = parser.Parse("document.pdf");

// Markdown — feed directly into your RAG chunking pipeline
Console.WriteLine(result.Markdown);

// JSON — OpenDataLoader-compatible, bounding boxes included
File.WriteAllText("output.json", result.Json);
```

## Features

| Feature | Status |
|---------|--------|
| XY-Cut++ reading order (multi-column) | ✅ |
| Heading hierarchy detection (font-based) | ✅ |
| Paragraph grouping | ✅ |
| Bounding box per element | ✅ |
| Markdown output | ✅ |
| JSON output (OpenDataLoader-compatible) | ✅ |
| Prompt injection filtering | ✅ |
| Invalid character replacement | ✅ |
| Sensitive text sanitization (optional) | ✅ |
| Table extraction (bordered) | 🔜 Phase 2 |
| List detection | 🔜 Phase 2 |
| Image extraction | 🔜 Phase 2 |
| Tagged PDF structure tree | 🔜 Phase 3 |
| Header/footer filtering | 🔜 Phase 2 |

## Output

### Markdown

```markdown
# Introduction

This paper presents a novel approach to...

## Related Work

Previous studies have shown that...
```

### JSON (OpenDataLoader-compatible)

```json
{
  "file name": "paper.pdf",
  "number of pages": 12,
  "kids": [
    {
      "type": "heading",
      "id": 1,
      "page number": 1,
      "bounding box": [72.0, 700.0, 540.0, 730.0],
      "heading level": 1,
      "content": "Introduction"
    }
  ]
}
```

## Architecture

```
PdfStruct
├── Models/          # Content element types (heading, paragraph, table, ...)
├── Analysis/        # XY-Cut++ layout analyzer, element classifier
├── Rendering/       # Markdown & JSON renderers
├── Safety/          # Prompt injection filtering, text sanitization
├── PdfStructParser  # Main entry point
└── PdfStructOptions # Configuration
```

Built on [PdfPig](https://github.com/UglyToad/PdfPig) (Apache-2.0) for low-level PDF access.

## Development

### Prerequisites

- .NET SDK `8.0.416` or a compatible feature-band roll-forward, as defined in [`global.json`](global.json)
- Windows, Linux, or macOS

### Build and test

```bash
dotnet restore PdfStruct.sln
dotnet build PdfStruct.sln -c Release --no-restore
dotnet test PdfStruct.sln -c Release --no-build
dotnet format PdfStruct.sln --verify-no-changes --no-restore
```

### Repository conventions

- C# language version is pinned to `12.0` in [`Directory.Build.props`](Directory.Build.props).
- Nullable reference types and implicit usings are enabled repo-wide.
- Package versions are centrally managed in [`Directory.Packages.props`](Directory.Packages.props).
- NuGet restore is scoped to `nuget.org` through [`NuGet.config`](NuGet.config).
- Text files use UTF-8 and LF line endings, enforced by [`.editorconfig`](.editorconfig) and [`.gitattributes`](.gitattributes).

## Roadmap

- **Phase 1** (current): Reading order, heading/paragraph classification, Markdown/JSON output
- **Phase 2**: Table detection, list detection, image extraction, header/footer filtering
- **Phase 3**: Tagged PDF support, borderless table detection

## License

[Apache License 2.0](LICENSE)
