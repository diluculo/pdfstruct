// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Safety;

namespace PdfStruct;

/// <summary>
/// Specifies the output format for PDF conversion.
/// </summary>
[Flags]
public enum OutputFormat
{
    /// <summary>Structured Markdown output for LLM context and RAG chunking.</summary>
    Markdown = 1,
    /// <summary>JSON with bounding boxes (OpenDataLoader-compatible).</summary>
    Json = 2,
    /// <summary>Both Markdown and JSON.</summary>
    Both = Markdown | Json
}

/// <summary>
/// Specifies how images should be handled during extraction.
/// </summary>
public enum ImageOutputMode
{
    /// <summary>Do not extract images.</summary>
    Off,
    /// <summary>Embed images as Base64 data URIs.</summary>
    Embedded,
    /// <summary>Save images as external files.</summary>
    External
}

/// <summary>
/// Configuration options for <see cref="PdfStructParser"/>.
/// </summary>
public sealed class PdfStructOptions
{
    /// <summary>Gets or sets the output format(s). Default: Markdown.</summary>
    public OutputFormat Format { get; set; } = OutputFormat.Markdown;

    /// <summary>Gets or sets image handling mode. Default: Off.</summary>
    public ImageOutputMode ImageOutput { get; set; } = ImageOutputMode.Off;

    /// <summary>Gets or sets the image format ("png" or "jpeg"). Default: "png".</summary>
    public string ImageFormat { get; set; } = "png";

    /// <summary>Gets or sets whether to use Tagged PDF structure tree when available. Default: true.</summary>
    public bool UseStructTree { get; set; } = true;

    /// <summary>Gets or sets whether to filter hidden text for prompt injection protection. Default: true.</summary>
    public bool FilterHiddenText { get; set; } = true;

    /// <summary>Gets or sets whether to mask common sensitive values in extracted text. Default: false.</summary>
    public bool SanitizeText { get; set; }

    /// <summary>
    /// Gets or sets the replacement for invalid extraction characters such as U+FFFD and NUL.
    /// Set to <c>null</c> to preserve them. Default: space.
    /// </summary>
    public string? InvalidCharacterReplacement { get; set; } = " ";

    /// <summary>Gets the regex-based sanitization rules used when <see cref="SanitizeText"/> is enabled.</summary>
    public List<TextSanitizationRule> SanitizationRules { get; } = TextSanitizer.CreateDefaultRules();

    /// <summary>Gets or sets whether to exclude headers/footers. Default: true.</summary>
    public bool ExcludeHeadersFooters { get; set; } = true;

    /// <summary>Gets or sets the minimum horizontal gap ratio for XY-Cut column detection. Default: 0.01.</summary>
    public double MinGapRatioX { get; set; } = 0.01;

    /// <summary>Gets or sets the minimum vertical gap ratio for XY-Cut row detection. Default: 0.005.</summary>
    public double MinGapRatioY { get; set; } = 0.005;

    /// <summary>Gets or sets the font size ratio threshold for heading classification. Default: 1.2.</summary>
    public double HeadingSizeThreshold { get; set; } = 1.2;
}
