using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace api.Managers.KnowledgeBase;

// Word → Markdown from the document's own structure: heading styles, bulleted and numbered lists, tables,
// bold/italic and links. Images, footnotes, headers, footers and the table of contents are left out.
public static class DocxMarkdownConverter
{
    // Styles are based on other styles; this guards against a broken (circular) chain.
    private const int MaxStyleDepth = 10;

    // Word's outline level 9 means body text.
    private const int BodyTextOutlineLevel = 9;

    public static string Convert(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var mainPart = document.MainDocumentPart;
        var body = mainPart?.Document?.Body;
        if (mainPart is null || body is null)
        {
            return string.Empty;
        }

        // Style ids are localized ("Overskrift1" in Danish Word), but the built-in style names are always English
        // ("heading 1"), so headings are found by name.
        var styles = (mainPart.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
            .Where(style => style.StyleId?.Value is not null)
            .GroupBy(style => style.StyleId!.Value!)
            .ToDictionary(group => group.Key, group => group.First());

        // A document with a Title gets it as # and its headings one level down (Heading 1 → ##).
        var hasTitle = body.Descendants<Paragraph>()
            .Any(paragraph => GetStyleChain(paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value, styles).Any(style => StyleName(style) == "title"));

        var blocks = new List<(string Markdown, bool IsListItem)>();
        AppendBlocks(body.Elements(), blocks, mainPart, styles, hasTitle ? 1 : 0);
        return MarkdownFormatter.Join(blocks);
    }

    private static void AppendBlocks(
        IEnumerable<OpenXmlElement> elements,
        List<(string Markdown, bool IsListItem)> blocks,
        MainDocumentPart mainPart,
        Dictionary<string, Style> styles,
        int headingOffset)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case Paragraph paragraph when ConvertParagraph(paragraph, mainPart, styles, headingOffset) is { } block:
                    blocks.Add(block);
                    break;
                case Table table when ConvertTable(table, mainPart) is { Length: > 0 } markdown:
                    blocks.Add((markdown, false));
                    break;
                case SdtBlock contentControl:
                    AppendBlocks(contentControl.SdtContentBlock?.Elements() ?? [], blocks, mainPart, styles, headingOffset);
                    break;
            }
        }
    }

    private static (string Markdown, bool IsListItem)? ConvertParagraph(
        Paragraph paragraph, MainDocumentPart mainPart, Dictionary<string, Style> styles, int headingOffset)
    {
        var properties = paragraph.ParagraphProperties;
        var styleChain = GetStyleChain(properties?.ParagraphStyleId?.Val?.Value, styles);
        if (styleChain.Any(style => StyleName(style).StartsWith("toc ", StringComparison.Ordinal)))
        {
            return null;
        }

        if (GetHeadingLevel(properties, styleChain) is { } level)
        {
            var headingText = string.Concat(CollectSegments(paragraph, mainPart).Select(segment => segment.Text));
            var markdownLevel = styleChain.Any(style => StyleName(style) == "title") ? 1 : level + headingOffset;
            return string.IsNullOrWhiteSpace(headingText) ? null : (MarkdownFormatter.Heading(markdownLevel, headingText), false);
        }

        var text = ConvertInline(paragraph, mainPart, "  \n").Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (GetList(properties, styleChain, mainPart) is { } list)
        {
            // Four spaces per level nest correctly under both "- " and "1. " items.
            return ($"{new string(' ', 4 * list.Level)}{(list.IsOrdered ? "1." : "-")} {text}", true);
        }

        return (text, false);
    }

    // The first row is the header. Paragraphs inside a cell are separated by <br>.
    private static string ConvertTable(Table table, MainDocumentPart mainPart)
    {
        var rows = table.Elements<TableRow>()
            .Select(row => (IReadOnlyList<string>)row.Elements<TableCell>()
                .Select(cell => string.Join("<br>", cell.Descendants<Paragraph>()
                    .Select(paragraph => ConvertInline(paragraph, mainPart, "<br>").Trim())
                    .Where(text => text.Length > 0)))
                .ToList())
            .Where(row => row.Count > 0)
            .ToList();

        return MarkdownFormatter.Table(rows);
    }

    private static List<Style> GetStyleChain(string? styleId, Dictionary<string, Style> styles)
    {
        var chain = new List<Style>();
        for (var id = styleId; id is not null && chain.Count < MaxStyleDepth && styles.TryGetValue(id, out var style); id = style.BasedOn?.Val?.Value)
        {
            chain.Add(style);
        }

        return chain;
    }

    private static string StyleName(Style style) => style.StyleName?.Val?.Value?.ToLowerInvariant() ?? string.Empty;

    private static int? GetHeadingLevel(ParagraphProperties? properties, List<Style> styleChain)
    {
        if (properties?.OutlineLevel?.Val?.Value is { } outlineLevel && outlineLevel < BodyTextOutlineLevel)
        {
            return outlineLevel + 1;
        }

        foreach (var style in styleChain)
        {
            var name = StyleName(style);
            if (name == "title")
            {
                return 1;
            }

            if (name.StartsWith("heading ", StringComparison.Ordinal) && int.TryParse(name["heading ".Length..], out var level))
            {
                return level;
            }

            if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } styleOutlineLevel && styleOutlineLevel < BodyTextOutlineLevel)
            {
                return styleOutlineLevel + 1;
            }
        }

        return null;
    }

    // Numbering is set on the paragraph, or on its style ("List Bullet", "List Number"). The level's format
    // decides bullet or number.
    private static (int Level, bool IsOrdered)? GetList(ParagraphProperties? properties, List<Style> styleChain, MainDocumentPart mainPart)
    {
        var numberingProperties = properties?.NumberingProperties
            ?? styleChain.Select(style => style.StyleParagraphProperties?.NumberingProperties).FirstOrDefault(numbering => numbering is not null);
        var numberingId = numberingProperties?.NumberingId?.Val?.Value;
        if (numberingId is null or 0)
        {
            return null;
        }

        var level = Math.Clamp(numberingProperties?.NumberingLevelReference?.Val?.Value ?? 0, 0, 8);
        var numbering = mainPart.NumberingDefinitionsPart?.Numbering;
        var abstractNumberId = numbering?.Elements<NumberingInstance>()
            .FirstOrDefault(instance => instance.NumberID?.Value == numberingId)?.AbstractNumId?.Val?.Value;
        var format = numbering?.Elements<AbstractNum>()
            .FirstOrDefault(abstractNumber => abstractNumber.AbstractNumberId?.Value == abstractNumberId)?
            .Elements<Level>()
            .FirstOrDefault(numberingLevel => numberingLevel.LevelIndex?.Value == level)?
            .NumberingFormat?.Val?.Value;

        if (format is { } value && value.Equals(NumberFormatValues.None))
        {
            return null;
        }

        return (level, format is { } numberFormat && !numberFormat.Equals(NumberFormatValues.Bullet));
    }

    // Runs with the same formatting and link are merged first, so a word split over several runs gets one marker.
    private static string ConvertInline(OpenXmlElement paragraph, MainDocumentPart mainPart, string lineBreak)
    {
        var segments = CollectSegments(paragraph, mainPart);
        var markdown = new StringBuilder();
        for (var start = 0; start < segments.Count;)
        {
            var first = segments[start];
            var text = new StringBuilder();
            var end = start;
            for (; end < segments.Count && segments[end].Bold == first.Bold && segments[end].Italic == first.Italic && segments[end].Link == first.Link; end++)
            {
                text.Append(segments[end].Text);
            }

            markdown.Append(Format(text.ToString(), first.Bold, first.Italic, first.Link, lineBreak));
            start = end;
        }

        return markdown.ToString();
    }

    private static List<(string Text, bool Bold, bool Italic, string? Link)> CollectSegments(OpenXmlElement paragraph, MainDocumentPart mainPart)
    {
        var segments = new List<(string Text, bool Bold, bool Italic, string? Link)>();
        CollectSegments(paragraph, mainPart, null, segments);
        return segments;
    }

    private static void CollectSegments(OpenXmlElement element, MainDocumentPart mainPart, string? link, List<(string Text, bool Bold, bool Italic, string? Link)> segments)
    {
        foreach (var child in element.ChildElements)
        {
            switch (child)
            {
                case Run run:
                    if (RunText(run) is { Length: > 0 } text)
                    {
                        var runProperties = run.RunProperties;
                        segments.Add((text, IsOn(runProperties?.Bold), IsOn(runProperties?.Italic), link));
                    }

                    break;
                case Hyperlink hyperlink:
                    var url = hyperlink.Id?.Value is { } relationshipId
                        ? mainPart.HyperlinkRelationships.FirstOrDefault(relationship => relationship.Id == relationshipId)?.Uri.ToString()
                        : null;
                    CollectSegments(hyperlink, mainPart, url ?? link, segments);
                    break;
                case ParagraphProperties or DeletedRun:
                    break;
                default:
                    // Inserted (tracked) runs, content controls, fields, smart tags …
                    if (child.HasChildren)
                    {
                        CollectSegments(child, mainPart, link, segments);
                    }

                    break;
            }
        }
    }

    private static string RunText(Run run)
    {
        var text = new StringBuilder();
        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case Text value:
                    text.Append(value.Text);
                    break;
                case TabChar:
                    text.Append(' ');
                    break;
                case Break or CarriageReturn:
                    text.Append('\n');
                    break;
                case NoBreakHyphen:
                    text.Append('-');
                    break;
            }
        }

        return text.ToString();
    }

    private static bool IsOn(OnOffType? value) => value is not null && (value.Val is null || value.Val.Value);

    // Markers go around the text without its outer spaces ("**word** ", not "**word **"), or Markdown ignores them.
    private static string Format(string raw, bool bold, bool italic, string? link, string lineBreak)
    {
        var core = raw.Trim();
        if (core.Length == 0)
        {
            return raw.Contains('\n') ? lineBreak : raw.Length > 0 ? " " : string.Empty;
        }

        var leading = raw[..raw.IndexOf(core[0])];
        var trailing = raw[(raw.LastIndexOf(core[^1]) + 1)..];
        var marker = bold && italic ? "***" : bold ? "**" : italic ? "*" : string.Empty;
        var formatted = marker + MarkdownFormatter.Escape(core).Replace("\n", lineBreak) + marker;
        if (link is not null)
        {
            formatted = $"[{formatted}]({link.Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29")})";
        }

        return Whitespace(leading, lineBreak) + formatted + Whitespace(trailing, lineBreak);
    }

    private static string Whitespace(string whitespace, string lineBreak) =>
        whitespace.Contains('\n') ? lineBreak : whitespace.Length > 0 ? " " : string.Empty;
}
