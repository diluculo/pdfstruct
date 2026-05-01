// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// Line-level intermediate representation produced by the word-to-line
/// grouping stage and consumed by paragraph merging, list detection, and
/// the running-furniture filter. Carries the geometric and typographic
/// signals each downstream stage needs without exposing word-level detail.
/// </summary>
/// <param name="BoundingBox">Axis-aligned bounding box in PDF user space.</param>
/// <param name="Text">Visible text content of the line.</param>
/// <param name="FontName">Predominant font name across the line.</param>
/// <param name="FontSize">Predominant font size in points.</param>
/// <param name="IsBold">Whether the predominant font signals bold weight.</param>
/// <param name="BaselineY">Baseline y-coordinate, normalised so that horizontal lines have a stable value across glyph orientations.</param>
/// <param name="AvgHeight">Average glyph height, used as a proxy for typical inter-line spacing.</param>
internal readonly record struct TextLineBlock(
    BoundingBox BoundingBox,
    string Text,
    string FontName,
    double FontSize,
    bool IsBold,
    double BaselineY,
    double AvgHeight)
{
    /// <summary>Convenience accessor for <c>BoundingBox.Left</c>.</summary>
    public double Left => BoundingBox.Left;

    /// <summary>Convenience accessor for <c>BoundingBox.Right</c>.</summary>
    public double Right => BoundingBox.Right;

    /// <summary>Convenience accessor for <c>BoundingBox.Bottom</c>.</summary>
    public double Bottom => BoundingBox.Bottom;

    /// <summary>Convenience accessor for <c>BoundingBox.Top</c>.</summary>
    public double Top => BoundingBox.Top;

    /// <summary>Convenience accessor for <c>BoundingBox.Width</c>.</summary>
    public double Width => BoundingBox.Width;

    /// <summary>
    /// Lifts the line into a single-line <see cref="TextBlock"/>, used when
    /// the layout analyzer or the sanitizer must operate on the unmerged
    /// line stream.
    /// </summary>
    public TextBlock ToTextBlock() => new(
        BoundingBox,
        Text,
        FontName,
        FontSize,
        IsBold,
        LineCount: 1);
}
