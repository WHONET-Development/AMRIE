using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;

namespace AMRIE.WinUI.Common;

/// <summary>
/// Converts simple Markdown documentation (headings, dividers, tables, bullet lists, and inline formatting)
/// into native WinUI 3 controls for bundled notice dialogs.
/// </summary>
public static class MarkdownViewerHelper
{
    private static readonly Regex TableSeparatorRegex = new(@"^\|?(\s*:?-+:?\s*\|)+\s*:?-+:?\s*\|?$", RegexOptions.Compiled);
    private static readonly Regex InlineTokenRegex = new(
        @"\[(?<linkText>[^\]]+)\]\((?<linkUrl>[^)]+)\)|\*\*(?<bold>[^*]+)\*\*|`(?<code>[^`]+)`|\*(?<italic>[^*]+)\*",
        RegexOptions.Compiled);

    private static readonly SolidColorBrush DividerBrush = new(Color.FromArgb(48, 128, 128, 128));
    private static readonly SolidColorBrush TableBorderBrush = new(Color.FromArgb(64, 128, 128, 128));
    private static readonly SolidColorBrush TableHeaderBrush = new(Color.FromArgb(24, 128, 128, 128));
    private static readonly FontFamily MonospaceFont = new("Consolas");

    public static UIElement BuildMarkdownView(string markdown, string baseDirectory)
    {
        var root = new StackPanel { Spacing = 8 };
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        int i = 0;
        var paragraphBuffer = new List<string>();

        void FlushParagraph()
        {
            if (paragraphBuffer.Count == 0) return;
            string joined = string.Join(" ", paragraphBuffer).Trim();
            paragraphBuffer.Clear();
            if (!string.IsNullOrEmpty(joined))
            {
                root.Children.Add(CreateRichTextBlock(joined, baseDirectory, fontSize: 13.5));
            }
        }

        while (i < lines.Length)
        {
            string rawLine = lines[i];
            string trimmed = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph();
                i++;
                continue;
            }

            if (trimmed is "---" or "***" or "___")
            {
                FlushParagraph();
                root.Children.Add(new Border
                {
                    Height = 1,
                    Background = DividerBrush,
                    Margin = new Thickness(0, 6, 0, 6)
                });
                i++;
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                FlushParagraph();
                int level = 0;
                while (level < trimmed.Length && trimmed[level] == '#') level++;
                string headingText = trimmed[level..].Trim();

                double fontSize = level switch
                {
                    1 => 19,
                    2 => 15,
                    _ => 13.5
                };
                root.Children.Add(new TextBlock
                {
                    Text = StripInlineMarkers(headingText),
                    FontSize = fontSize,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    Margin = new Thickness(0, level == 1 ? 0 : 8, 0, 2)
                });
                i++;
                continue;
            }

            if (IsTableStart(lines, i))
            {
                FlushParagraph();
                var tableLines = new List<string>();
                while (i < lines.Length && lines[i].Trim().StartsWith('|'))
                {
                    tableLines.Add(lines[i].Trim());
                    i++;
                }
                root.Children.Add(BuildTable(tableLines, baseDirectory));
                continue;
            }

            if (IsBulletLine(rawLine, out int indentSpaces, out string bulletText))
            {
                FlushParagraph();
                int indentLevel = indentSpaces >= 2 ? 1 : 0;
                root.Children.Add(BuildBulletRow(bulletText, indentLevel, baseDirectory));
                i++;
                continue;
            }

            paragraphBuffer.Add(trimmed);
            i++;
        }

        FlushParagraph();
        return root;
    }

    private static bool IsTableStart(string[] lines, int index)
    {
        if (index + 1 >= lines.Length) return false;
        string header = lines[index].Trim();
        string separator = lines[index + 1].Trim();
        return header.StartsWith('|') && TableSeparatorRegex.IsMatch(separator);
    }

    private static bool IsBulletLine(string rawLine, out int indentSpaces, out string content)
    {
        indentSpaces = 0;
        while (indentSpaces < rawLine.Length && rawLine[indentSpaces] == ' ') indentSpaces++;
        string trimmed = rawLine[indentSpaces..];
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
        {
            content = trimmed[2..].Trim();
            return true;
        }
        content = string.Empty;
        return false;
    }

    private static UIElement BuildBulletRow(string text, int indentLevel, string baseDirectory)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            Margin = new Thickness(indentLevel * 18, 1, 0, 1)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var glyph = new TextBlock
        {
            Text = indentLevel == 0 ? "•" : "◦",
            FontSize = 13.5,
            Margin = new Thickness(2, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(glyph, 0);

        var body = CreateRichTextBlock(text, baseDirectory, fontSize: 13.5);
        Grid.SetColumn(body, 1);

        grid.Children.Add(glyph);
        grid.Children.Add(body);
        return grid;
    }

    private static UIElement BuildTable(List<string> tableLines, string baseDirectory)
    {
        var headers = ParseTableRow(tableLines[0]);
        var rows = new List<List<string>>();
        for (int r = 2; r < tableLines.Count; r++)
        {
            rows.Add(ParseTableRow(tableLines[r]));
        }

        int columnCount = headers.Count;
        var grid = new Grid();
        for (int c = 0; c < columnCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        int totalRows = rows.Count + 1;
        for (int r = 0; r < totalRows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // Header row
        for (int c = 0; c < columnCount; c++)
        {
            var cellText = new TextBlock
            {
                Text = StripInlineMarkers(headers[c]),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            var cellBorder = new Border
            {
                Background = TableHeaderBrush,
                BorderBrush = TableBorderBrush,
                BorderThickness = new Thickness(c == 0 ? 0 : 1, 0, 0, 1),
                Padding = new Thickness(8, 6, 8, 6),
                Child = cellText
            };
            Grid.SetRow(cellBorder, 0);
            Grid.SetColumn(cellBorder, c);
            grid.Children.Add(cellBorder);
        }

        // Data rows
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            bool isLast = r == rows.Count - 1;
            for (int c = 0; c < columnCount; c++)
            {
                string cellValue = c < row.Count ? row[c] : string.Empty;
                var cellBorder = new Border
                {
                    BorderBrush = TableBorderBrush,
                    BorderThickness = new Thickness(c == 0 ? 0 : 1, 0, 0, isLast ? 0 : 1),
                    Padding = new Thickness(8, 6, 8, 6),
                    Child = CreateRichTextBlock(cellValue, baseDirectory, fontSize: 12)
                };
                Grid.SetRow(cellBorder, r + 1);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        return new Border
        {
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 4, 0, 4),
            Child = grid
        };
    }

    private static List<string> ParseTableRow(string line)
    {
        string trimmed = line.Trim().Trim('|');
        var parts = trimmed.Split('|');
        var result = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            result.Add(part.Trim());
        }
        return result;
    }

    private static RichTextBlock CreateRichTextBlock(string text, string baseDirectory, double fontSize)
    {
        var rtb = new RichTextBlock
        {
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        var paragraph = new Paragraph();
        AddInlines(paragraph.Inlines, text, baseDirectory);
        rtb.Blocks.Add(paragraph);
        return rtb;
    }

    private static void AddInlines(InlineCollection inlines, string text, string baseDirectory, bool allowLinks = true)
    {
        int cursor = 0;
        foreach (Match match in InlineTokenRegex.Matches(text))
        {
            if (match.Index > cursor)
            {
                inlines.Add(new Run { Text = text[cursor..match.Index] });
            }

            if (match.Groups["linkText"].Success)
            {
                string label = match.Groups["linkText"].Value;
                if (allowLinks)
                {
                    string target = match.Groups["linkUrl"].Value.Trim();
                    AddLinkInline(inlines, label, target, baseDirectory);
                }
                else
                {
                    AddInlines(inlines, label, baseDirectory, allowLinks: false);
                }
            }
            else if (match.Groups["bold"].Success)
            {
                var bold = new Bold();
                AddInlines(bold.Inlines, match.Groups["bold"].Value, baseDirectory, allowLinks);
                inlines.Add(bold);
            }
            else if (match.Groups["code"].Success)
            {
                var span = new Span { FontFamily = MonospaceFont };
                span.Inlines.Add(new Run { Text = match.Groups["code"].Value });
                inlines.Add(span);
            }
            else if (match.Groups["italic"].Success)
            {
                var italic = new Italic();
                italic.Inlines.Add(new Run { Text = match.Groups["italic"].Value });
                inlines.Add(italic);
            }

            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            inlines.Add(new Run { Text = text[cursor..] });
        }
    }

    private static void AddLinkInline(InlineCollection inlines, string label, string target, string baseDirectory)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var webUri) &&
            (webUri.Scheme == Uri.UriSchemeHttp || webUri.Scheme == Uri.UriSchemeHttps))
        {
            var hyperlink = new Hyperlink { NavigateUri = webUri };
            AddInlines(hyperlink.Inlines, label, baseDirectory, allowLinks: false);
            inlines.Add(hyperlink);
            return;
        }

        string candidatePath = Path.GetFullPath(Path.Combine(baseDirectory, target.Replace('/', Path.DirectorySeparatorChar)));
        if (File.Exists(candidatePath))
        {
            var hyperlink = new Hyperlink();
            AddInlines(hyperlink.Inlines, label, baseDirectory, allowLinks: false);
            hyperlink.Click += async (_, _) =>
            {
                try
                {
                    await Launcher.LaunchUriAsync(new Uri(candidatePath));
                }
                catch { }
            };
            inlines.Add(hyperlink);
            return;
        }

        AddInlines(inlines, label, baseDirectory, allowLinks: false);
    }

    private static string StripInlineMarkers(string text)
        => text.Replace("**", string.Empty).Replace("`", string.Empty);
}
