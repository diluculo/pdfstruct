// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;
using SkiaSharp;
using UglyToad.PdfPig.Content;

namespace PdfStruct.Cli;

/// <summary>
/// Renders per-page PNG overlays of extracted layout for debugging.
/// Each output image redraws the PdfPig word layer in place and overlays
/// the bounding boxes of detected <see cref="ContentElement"/>s, color-coded
/// by element type and labeled <c>{id}:{type}</c>.
/// </summary>
internal static class DebugImageRenderer
{
    private const int TargetPageWidth = 1600;

    /// <summary>Renders one debug image per page of the supplied PDF.</summary>
    /// <param name="inputPdfPath">Path to the source PDF, opened to obtain page geometry and the word layer.</param>
    /// <param name="document">The parsed structured document whose elements are overlaid.</param>
    /// <param name="outputDirectory">Directory to write <c>page-NNN.png</c> files to. Created if it does not exist.</param>
    /// <returns>The output paths of every PNG written, in page order.</returns>
    public static IReadOnlyList<string> Render(
        string inputPdfPath,
        Models.PdfDocument document,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(inputPdfPath);
        var outputFiles = new List<string>(pdf.NumberOfPages);

        for (var pageNumber = 1; pageNumber <= pdf.NumberOfPages; pageNumber++)
        {
            var page = pdf.GetPage(pageNumber);
            var elements = document.Kids
                .Where(element => element.PageNumber == pageNumber)
                .OrderBy(element => element.Id)
                .ToList();

            var outputPath = Path.Combine(outputDirectory, $"page-{pageNumber:000}.png");
            RenderPage(outputPath, page, elements);
            outputFiles.Add(outputPath);
        }

        return outputFiles;
    }

    /// <summary>Renders a single page's overlay PNG to <paramref name="outputPath"/>.</summary>
    private static void RenderPage(
        string outputPath,
        Page page,
        IReadOnlyList<ContentElement> elements)
    {
        var pageWidth = page.Width;
        var pageHeight = page.Height;
        var scale = (float)Math.Min(2.0, TargetPageWidth / pageWidth);
        var width = Math.Max(1, (int)Math.Ceiling(pageWidth * scale));
        var height = Math.Max(1, (int)Math.Ceiling(pageHeight * scale));

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        DrawTextLayer(canvas, page, pageHeight, scale);
        DrawPageBorder(canvas, width, height);

        foreach (var element in elements)
        {
            DrawElement(canvas, element, pageHeight, scale);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.Create(outputPath);
        data.SaveTo(stream);
    }

    /// <summary>Draws the PdfPig word layer onto the canvas at the page-relative scale, flipping the y-axis from PDF space to canvas space.</summary>
    private static void DrawTextLayer(SKCanvas canvas, Page page, double pageHeight, float scale)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(30, 30, 30, 210),
            IsAntialias = true
        };

        foreach (var word in page.GetWords())
        {
            if (string.IsNullOrWhiteSpace(word.Text))
            {
                continue;
            }

            var pointSize = word.Letters.FirstOrDefault()?.PointSize ?? word.BoundingBox.Height;
            var fontSize = Math.Clamp((float)(pointSize * scale), 6f, 48f);
            using var font = new SKFont(SKTypeface.Default, fontSize);

            var x = (float)(word.BoundingBox.Left * scale);
            var y = (float)((pageHeight - word.BoundingBox.Bottom) * scale);
            canvas.DrawText(word.Text, x, y, SKTextAlign.Left, font, paint);
        }
    }

    /// <summary>Strokes a thin rectangle around the page bounds.</summary>
    private static void DrawPageBorder(SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(30, 30, 30),
            IsAntialias = true,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawRect(new SKRect(1, 1, width - 2, height - 2), paint);
    }

    /// <summary>Fills, strokes, and labels the bounding box of one structured element. Skips elements with a non-positive area on the canvas.</summary>
    private static void DrawElement(
        SKCanvas canvas,
        ContentElement element,
        double pageHeight,
        float scale)
    {
        var rect = ToCanvasRect(element.BoundingBox, pageHeight, scale);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var color = GetColor(element.Type);
        using var fill = new SKPaint
        {
            Color = color.WithAlpha(35),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        using var stroke = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            StrokeWidth = Math.Max(2, scale),
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawRect(rect, fill);
        canvas.DrawRect(rect, stroke);
        DrawLabel(canvas, rect, element, color);
    }

    /// <summary>Draws the <c>{id}:{type}</c> label tab above an element's bounding box.</summary>
    private static void DrawLabel(SKCanvas canvas, SKRect rect, ContentElement element, SKColor color)
    {
        var label = $"{element.Id}:{element.Type}";
        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        using var backgroundPaint = new SKPaint
        {
            Color = color.WithAlpha(220),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var textWidth = font.MeasureText(label);
        var labelRect = new SKRect(
            rect.Left,
            Math.Max(0, rect.Top - 18),
            rect.Left + textWidth + 8,
            Math.Max(18, rect.Top));

        canvas.DrawRect(labelRect, backgroundPaint);
        canvas.DrawText(label, labelRect.Left + 4, labelRect.Bottom - 4, SKTextAlign.Left, font, textPaint);
    }

    /// <summary>Converts a PDF-space bounding box (origin bottom-left) to a canvas-space rectangle (origin top-left), applying the supplied scale.</summary>
    private static SKRect ToCanvasRect(BoundingBox box, double pageHeight, float scale)
    {
        var left = (float)(box.Left * scale);
        var top = (float)((pageHeight - box.Top) * scale);
        var right = (float)(box.Right * scale);
        var bottom = (float)((pageHeight - box.Bottom) * scale);
        return new SKRect(left, top, right, bottom);
    }

    /// <summary>Returns the overlay color for a given element type. Falls back to teal for unrecognized types.</summary>
    private static SKColor GetColor(string elementType) =>
        elementType switch
        {
            "heading" => new SKColor(214, 69, 65),
            "paragraph" => new SKColor(45, 120, 210),
            "table" => new SKColor(38, 166, 91),
            "list" => new SKColor(142, 68, 173),
            "image" => new SKColor(90, 90, 90),
            "caption" => new SKColor(230, 126, 34),
            "header" or "footer" => new SKColor(120, 90, 50),
            _ => new SKColor(20, 150, 140)
        };
}
