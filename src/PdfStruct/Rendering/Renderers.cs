// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PdfStruct.Models;

namespace PdfStruct.Rendering;

/// <summary>
/// Defines a renderer that converts a parsed PDF document to a specific output format.
/// </summary>
public interface IDocumentRenderer
{
    /// <summary>Renders the document to a string in the target format.</summary>
    string Render(Models.PdfDocument document);
}

/// <summary>
/// Renders a <see cref="Models.PdfDocument"/> to Markdown, preserving heading hierarchy,
/// table structure, and list formatting. Ideal for LLM context and RAG chunking.
/// </summary>
public sealed partial class MarkdownRenderer : IDocumentRenderer
{
    /// <inheritdoc />
    public string Render(Models.PdfDocument document)
    {
        var sb = new StringBuilder();
        foreach (var element in document.Kids)
        {
            RenderElement(element, sb);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static void RenderElement(ContentElement element, StringBuilder sb)
    {
        switch (element)
        {
            case HeadingElement h:
                sb.Append(new string('#', Math.Clamp(h.HeadingLevel, 1, 6)));
                sb.Append(' ');
                sb.AppendLine(h.Text.Content);
                break;

            case ParagraphElement p:
                sb.AppendLine(BoldArticleAnchor(p.Text.Content));
                break;

            case TableElement t:
                RenderTable(t, sb);
                break;

            case ListElement l:
                RenderList(l, sb);
                break;

            case ImageElement img:
                sb.AppendLine(img.Source is not null ? $"![image]({img.Source})" : "[Image]");
                break;

            case CaptionElement cap:
                sb.Append('*').Append(cap.Text.Content).AppendLine("*");
                break;
        }
    }

    private static void RenderTable(TableElement table, StringBuilder sb)
    {
        foreach (var row in table.Rows)
        {
            sb.Append('|');
            foreach (var cell in row.Cells)
            {
                var text = string.Join(" ", cell.Kids.OfType<ParagraphElement>().Select(p => p.Text.Content));
                sb.Append($" {(string.IsNullOrEmpty(text) ? " " : text)} |");
            }
            sb.AppendLine();

            if (row.RowNumber == 1)
            {
                sb.Append('|');
                for (int i = 0; i < row.Cells.Count; i++) sb.Append(" --- |");
                sb.AppendLine();
            }
        }
    }

    private static void RenderList(ListElement list, StringBuilder sb)
    {
        var ordered = list.NumberingStyle is "ordered" or "decimal" or "roman";
        for (int i = 0; i < list.ListItems.Count; i++)
        {
            sb.Append(ordered ? $"{i + 1}." : "-");
            sb.Append(' ').AppendLine(list.ListItems[i].Text.Content);
        }
    }

    /// <summary>
    /// Wraps a Korean article anchor (<c>제N조</c>) at the start of a paragraph
    /// in bold so the article number visually stands out without becoming a
    /// heading in the document tree.
    /// </summary>
    private static string BoldArticleAnchor(string content)
    {
        var match = ArticleAnchorRegex().Match(content);
        if (!match.Success || match.Index != 0) return content;

        var anchor = match.Value.TrimEnd();
        var rest = content[match.Length..].TrimStart();
        return rest.Length == 0 ? $"**{anchor}**" : $"**{anchor}** {rest}";
    }

    [GeneratedRegex(@"^제\s*\d+\s*조(?=\s|$)", RegexOptions.Compiled)]
    private static partial Regex ArticleAnchorRegex();
}

/// <summary>
/// Renders a <see cref="Models.PdfDocument"/> to JSON compatible with OpenDataLoader PDF's schema.
/// </summary>
public sealed class JsonRenderer : IDocumentRenderer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public string Render(Models.PdfDocument document)
    {
        var output = new Dictionary<string, object?>
        {
            ["file name"] = document.FileName,
            ["number of pages"] = document.NumberOfPages,
            ["author"] = document.Author,
            ["title"] = document.Title,
            ["creation date"] = document.CreationDate,
            ["modification date"] = document.ModificationDate,
            ["kids"] = document.Kids.Select(ToOdlElement).ToList()
        };
        return JsonSerializer.Serialize(output, s_options);
    }

    private static Dictionary<string, object?> ToOdlElement(ContentElement element)
    {
        var dict = new Dictionary<string, object?>
        {
            ["type"] = element.Type,
            ["id"] = element.Id,
            ["page number"] = element.PageNumber,
            ["bounding box"] = element.BoundingBox.ToArray()
        };

        switch (element)
        {
            case HeadingElement h:
                dict["level"] = h.Level;
                dict["heading level"] = h.HeadingLevel;
                AddText(dict, h.Text);
                break;
            case ParagraphElement p:
                AddText(dict, p.Text);
                break;
            case TableElement t:
                dict["number of rows"] = t.NumberOfRows;
                dict["number of columns"] = t.NumberOfColumns;
                if (t.PreviousTableId.HasValue) dict["previous table id"] = t.PreviousTableId;
                if (t.NextTableId.HasValue) dict["next table id"] = t.NextTableId;
                dict["rows"] = t.Rows.Select(r => new Dictionary<string, object?>
                {
                    ["type"] = "table row",
                    ["row number"] = r.RowNumber,
                    ["cells"] = r.Cells.Select(c => new Dictionary<string, object?>
                    {
                        ["type"] = "table cell",
                        ["row number"] = c.RowNumber,
                        ["column number"] = c.ColumnNumber,
                        ["row span"] = c.RowSpan,
                        ["column span"] = c.ColumnSpan,
                        ["page number"] = c.PageNumber,
                        ["bounding box"] = c.BoundingBox.ToArray(),
                        ["kids"] = c.Kids.Select(ToOdlElement).ToList()
                    }).ToList()
                }).ToList();
                break;
            case ImageElement img:
                if (img.Source is not null) dict["source"] = img.Source;
                if (img.Data is not null) dict["data"] = img.Data;
                dict["format"] = img.Format;
                break;
            case CaptionElement cap:
                AddText(dict, cap.Text);
                if (cap.LinkedContentId.HasValue) dict["linked content id"] = cap.LinkedContentId;
                break;
        }
        return dict;
    }

    private static void AddText(Dictionary<string, object?> dict, TextProperties text)
    {
        dict["font"] = text.Font;
        dict["font size"] = text.FontSize;
        dict["text color"] = text.TextColor;
        dict["content"] = text.Content;
        if (text.HiddenText) dict["hidden text"] = true;
    }
}
