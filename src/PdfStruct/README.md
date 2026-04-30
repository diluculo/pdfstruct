# PdfStruct

PDF layout extraction for .NET RAG pipelines.

PdfStruct parses PDFs into structured Markdown or JSON with reading order, semantic text elements, and bounding boxes. It is built on PdfPig and targets `net8.0`.

```csharp
using PdfStruct;

var parser = new PdfStructParser(new PdfStructOptions
{
    Format = OutputFormat.Both,
    SanitizeText = true
});

var result = parser.Parse("document.pdf");

Console.WriteLine(result.Markdown);
File.WriteAllText("document.json", result.Json);
```

Current alpha features include XY-Cut reading order, heading and paragraph detection, Markdown output, JSON output, prompt-injection filtering, invalid character replacement, and optional sensitive text sanitization.

See the repository README for full docs and roadmap.
