// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Analysis;

namespace PdfStruct.Safety;

/// <summary>
/// Filters hidden text blocks that may contain prompt injection attacks.
/// Detects transparent text, zero-size fonts, and off-page content.
/// </summary>
public static class PromptInjectionFilter
{
    /// <summary>
    /// Filters out text blocks likely to be hidden or malicious.
    /// </summary>
    /// <param name="blocks">The input text blocks.</param>
    /// <param name="pageWidth">The page width in PDF points (for off-page detection).</param>
    /// <param name="pageHeight">The page height in PDF points.</param>
    /// <returns>Filtered list of visible, legitimate text blocks.</returns>
    public static List<TextBlock> Filter(
        IReadOnlyList<TextBlock> blocks,
        double pageWidth = 612,
        double pageHeight = 792)
    {
        return blocks.Where(b =>
        {
            // Zero-size or nearly invisible font
            if (b.FontSize < 1.0) return false;

            // Empty content
            if (string.IsNullOrWhiteSpace(b.Text)) return false;

            // Entirely off-page
            if (b.BoundingBox.Right < 0 || b.BoundingBox.Left > pageWidth) return false;
            if (b.BoundingBox.Top < 0 || b.BoundingBox.Bottom > pageHeight) return false;

            return true;
        }).ToList();
    }
}
