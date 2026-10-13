using System.Text;
using System.Text.RegularExpressions;

namespace api.Managers.KnowledgeBase;

// Markdown building blocks shared by the converters (TextMarkdownConverter, DocxMarkdownConverter,
// PdfMarkdownConverter). A document is built as a list of blocks; list items are kept together by Join.
// Text from a file is always escaped, so it renders exactly as written.
public static partial class MarkdownFormatter
{
    private const string NewLine = "\n";

    // Bump when a converter changes the Markdown it makes: documents made with an older version get it again in the
    // background (KnowledgeBaseManager.ProcessNextDocumentAsync).
    public const int Version = 2;

    // Bullets as they appear in plain text, PDFs and OCR output (incl. Wingdings' private-use bullet U+F0B7).
    [GeneratedRegex(@"^[•◦▪▫●○■□►▸‣⁃·\uF0B7\uF0A7\-–—*+]\s+(?<text>\S.*)$")]
    private static partial Regex BulletLineRegex();

    [GeneratedRegex(@"^(?<number>\d{1,3})[.)]\s+(?<text>\S.*)$")]
    private static partial Regex NumberedLineRegex();

    // A line that Markdown would read as a heading, quote, list item or setext underline.
    [GeneratedRegex(@"^(#{1,6}(\s|$)|>|[-+*]\s|=+\s*$|-+\s*$)")]
    private static partial Regex BlockStartRegex();

    [GeneratedRegex(@"^(?<number>\d+)(?<marker>[.)])(?=\s|$)")]
    private static partial Regex NumberedStartRegex();

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex BlankLinesRegex();

    // "Label: value", e.g. "Almen tilstand: Fin" or "Vægt i kg: 272". A time like "13:20" has no space after the colon.
    [GeneratedRegex(@"^(?<label>\p{Lu}[^:]{0,40}?):\s+(?<value>\S.*)$")]
    private static partial Regex FieldLineRegex();

    // Page counters and page numbers on their own line: "3", "1/1", "Side 3 af 12", "Page 3 of 12", "- 3 -".
    [GeneratedRegex(@"^(-\s*)?((side|page|s\.)\s*)?\d{1,4}(\s*(/|af|of)\s*\d{1,4})?(\s*-)?$", RegexOptions.IgnoreCase)]
    private static partial Regex PageNumberRegex();

    // A label (section title, lead-in) is short, starts with a capital letter, has no digits (those are addresses,
    // dates and phone numbers) and doesn't end like a sentence.
    private const int MaxLabelCharacters = 60;
    private const int MaxLabelWords = 8;

    // A label above wrapped text is clearly shorter than the line after it (otherwise it's just a wrapped line), or
    // stands above a bare value such as "17,7".
    private const double MaxLabelShareOfNextLine = 0.6;

    // A page header/footer: one of a page's first or last lines, found on at least this share of pages (and at least
    // this many). Only the outermost lines, so a title under the header (a note's type, say) is never taken for it.
    private const int PageEdgeLines = 3;
    private const double RepeatedPageLineShare = 0.3;
    private const int MinRepeatedPageLines = 3;

    // OCR reads the same footer a little differently on each page ("https" as "htips", a page counter glued on), so
    // lines count as the same when their letters' bigrams overlap this much (Dice coefficient).
    private const double SameLineSimilarity = 0.85;

    // Plain text (a .txt file, OCR output, a PDF page without layout) as paragraphs, labels, fields and lists. Blank
    // lines separate paragraphs; the lines inside one are joined, since they are usually wrapped text.
    public static string FromPlainText(string text)
    {
        var blocks = new List<(string Markdown, bool IsListItem)>();
        AppendPlainText(blocks, text);
        return Join(blocks);
    }

    public static void AppendPlainText(List<(string Markdown, bool IsListItem)> blocks, string text)
    {
        foreach (var paragraph in BlankLinesRegex().Split(NormalizeNewLines(text)))
        {
            AppendLines(blocks, paragraph.Split('\n'), detectLabels: true);
        }
    }

    // The lines of one paragraph or PDF text block. A line starting with a bullet or a number starts a list item;
    // other lines continue the current paragraph or item. With detectLabels (text without its own layout, such as
    // OCR output), "Label: value" lines become "**Label:** value" paragraphs, and a short title line on its own or
    // above longer text becomes a bold line. PDF text blocks get their titles from font sizes instead.
    public static void AppendLines(List<(string Markdown, bool IsListItem)> blocks, IEnumerable<string> lines, bool detectLabels = false)
    {
        var trimmed = lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        StringBuilder? current = null;
        var prefix = string.Empty;
        var isListItem = false;

        void Flush()
        {
            if (current is { Length: > 0 })
            {
                blocks.Add((prefix + Escape(current.ToString()), isListItem));
            }

            current = null;
        }

        void Start(string markdownPrefix, string text, bool listItem)
        {
            Flush();
            prefix = markdownPrefix;
            current = new StringBuilder(text);
            isListItem = listItem;
        }

        for (var index = 0; index < trimmed.Count; index++)
        {
            var line = trimmed[index];
            if (BulletLineRegex().Match(line) is { Success: true } bullet)
            {
                Start("- ", bullet.Groups["text"].Value, listItem: true);
            }
            else if (NumberedLineRegex().Match(line) is { Success: true } numbered)
            {
                Start($"{numbered.Groups["number"].Value}. ", numbered.Groups["text"].Value, listItem: true);
            }
            else if (detectLabels && FieldLineRegex().Match(line) is { Success: true } field)
            {
                Start($"**{Escape(field.Groups["label"].Value.Trim())}:** ", field.Groups["value"].Value, listItem: false);
            }
            else if (detectLabels && current is null && IsLabel(line, index + 1 < trimmed.Count ? trimmed[index + 1] : null))
            {
                blocks.Add(($"**{Escape(line)}**", false));
            }
            else if (current is null)
            {
                Start(string.Empty, line, listItem: false);
            }
            else
            {
                AppendWrappedLine(current, line);
            }
        }

        Flush();
    }

    public static bool IsPageNumber(string line) => PageNumberRegex().IsMatch(line.Trim());

    // OCR'ed pages (scans, multi-page TIFFs) repeat their header and footer on every page: printed-at stamps, names,
    // addresses, URLs. Lines among a page's first and last few that recur on many pages are kept the first time only;
    // page numbers are dropped. Lines are compared on their letters only and by similarity, so dates, page counters
    // and OCR noise don't make a repeated header look new.
    public static List<string> RemoveRepeatedPageLines(IReadOnlyList<string> pages)
    {
        var pageLines = pages.Select(page => NormalizeNewLines(page).Split('\n')).ToList();
        var clusters = new List<HashSet<int>>();
        var pagesPerCluster = new List<HashSet<int>>();

        // The cluster (group of near-identical lines) of every edge line, or -1 for lines without letters.
        var edgeClusters = pageLines.Select((lines, pageIndex) => EdgeLineIndexes(lines).ToDictionary(index => index, index =>
        {
            var bigrams = LetterBigrams(lines[index]);
            if (bigrams.Count == 0)
            {
                return -1;
            }

            var cluster = clusters.FindIndex(existing => Dice(existing, bigrams) >= SameLineSimilarity);
            if (cluster < 0)
            {
                clusters.Add(bigrams);
                pagesPerCluster.Add([]);
                cluster = clusters.Count - 1;
            }

            pagesPerCluster[cluster].Add(pageIndex);
            return cluster;
        })).ToList();

        var pagesWithText = pageLines.Count(lines => lines.Any(line => line.Trim().Length > 0));
        var minPages = Math.Max(MinRepeatedPageLines, pagesWithText * RepeatedPageLineShare);
        var seen = new HashSet<int>();
        return pageLines.Select((lines, pageIndex) => string.Join('\n', lines.Where((line, index) =>
        {
            if (!edgeClusters[pageIndex].TryGetValue(index, out var cluster))
            {
                return true;
            }

            if (IsPageNumber(line))
            {
                return false;
            }

            return cluster < 0 || pagesPerCluster[cluster].Count < minPages || seen.Add(cluster);
        }))).ToList();
    }

    private static IEnumerable<int> EdgeLineIndexes(string[] lines)
    {
        var nonEmpty = Enumerable.Range(0, lines.Length).Where(index => lines[index].Trim().Length > 0).ToList();
        return nonEmpty.Take(PageEdgeLines).Concat(nonEmpty.TakeLast(PageEdgeLines)).Distinct();
    }

    // Pairs of neighbouring letters, lowercased. Digits, spaces and punctuation are left out.
    private static HashSet<int> LetterBigrams(string line)
    {
        var letters = line.ToLowerInvariant().Where(char.IsLetter).ToArray();
        var bigrams = new HashSet<int>();
        for (var index = 1; index < letters.Length; index++)
        {
            bigrams.Add((letters[index - 1] << 16) | letters[index]);
        }

        return bigrams;
    }

    private static double Dice(HashSet<int> first, HashSet<int> second) =>
        2.0 * first.Count(second.Contains) / (first.Count + second.Count);

    private static bool IsLabel(string line, string? nextLine) =>
        line.Length <= MaxLabelCharacters &&
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= MaxLabelWords &&
        char.IsUpper(line[0]) &&
        line.Count(char.IsLetter) >= 2 &&
        !line.Any(char.IsDigit) &&
        line[^1] is not ('.' or ',' or ';' or '!' or '?') &&
        (nextLine is null || line.Length < nextLine.Length * MaxLabelShareOfNextLine || !nextLine.Any(char.IsLetter));

    public static bool IsListLine(string line) =>
        BulletLineRegex().IsMatch(line.Trim()) || NumberedLineRegex().IsMatch(line.Trim());

    // Consecutive items of the same kind of list stay together; every other block is separated by a blank line.
    public static string Join(IReadOnlyList<(string Markdown, bool IsListItem)> blocks)
    {
        var markdown = new StringBuilder();
        bool? listIsOrdered = null;
        for (var index = 0; index < blocks.Count; index++)
        {
            var (text, isListItem) = blocks[index];
            var isNested = isListItem && text.StartsWith(' ');
            var isOrdered = isListItem && !isNested && char.IsDigit(text[0]);
            if (index > 0)
            {
                // Nested items belong to the list above them; a top-level item continues it only if it's the same kind.
                var sameList = isListItem && blocks[index - 1].IsListItem && (isNested || listIsOrdered == isOrdered);
                markdown.Append(sameList ? NewLine : NewLine + NewLine);
            }

            if (isListItem && !isNested)
            {
                listIsOrdered = isOrdered;
            }

            markdown.Append(blocks[index].Markdown);
        }

        return markdown.ToString();
    }

    public static string Heading(int level, string text) =>
        $"{new string('#', Math.Clamp(level, 1, 6))} {Escape(CollapseWhitespace(text))}";

    // Cells are Markdown already (escaped). The first row is the header; short rows are padded.
    public static string Table(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        if (columnCount == 0)
        {
            return string.Empty;
        }

        var markdown = new StringBuilder();
        for (var index = 0; index < rows.Count; index++)
        {
            var cells = Enumerable.Range(0, columnCount).Select(column => column < rows[index].Count ? TableCell(rows[index][column]) : string.Empty);
            markdown.Append("| ").AppendJoin(" | ", cells).Append(" |").Append(NewLine);
            if (index == 0)
            {
                markdown.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", columnCount))).Append(NewLine);
            }
        }

        return markdown.ToString().TrimEnd('\n');
    }

    // The fence is longer than any run of backticks in the text, so the text can't close it early.
    public static string CodeBlock(string text, string language)
    {
        var longestRun = 0;
        var run = 0;
        foreach (var character in text)
        {
            run = character == '`' ? run + 1 : 0;
            longestRun = Math.Max(longestRun, run);
        }

        var fence = new string('`', Math.Max(3, longestRun + 1));
        return fence + language + NewLine + NormalizeNewLines(text).TrimEnd('\n') + NewLine + fence;
    }

    // Escapes what Markdown would otherwise read as formatting, links or HTML, and a block marker at the start.
    public static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length + 8);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var isIntraword = index > 0 && index < text.Length - 1 && char.IsLetterOrDigit(text[index - 1]) && char.IsLetterOrDigit(text[index + 1]);
            if (character is '\\' or '`' or '*' or '[' or ']' or '<' || (character == '_' && !isIntraword))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        var result = escaped.ToString();
        if (NumberedStartRegex().Match(result) is { Success: true } numbered)
        {
            return numbered.Groups["number"].Value + "\\" + result[numbered.Groups["number"].Length..];
        }

        return BlockStartRegex().IsMatch(result) ? "\\" + result : result;
    }

    public static string NormalizeNewLines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static string CollapseWhitespace(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Pipes would split the cell, and a table row must stay on one line.
    private static string TableCell(string markdown) =>
        NormalizeNewLines(markdown).Replace("|", "\\|").Replace("\n", "<br>").Trim();

    // A word broken over two lines ("kommune-" + "plan") is joined again; other lines are joined with a space.
    private static void AppendWrappedLine(StringBuilder paragraph, string line)
    {
        if (paragraph.Length >= 2 && paragraph[^1] == '-' && char.IsLetter(paragraph[^2]) && char.IsLower(line[0]))
        {
            paragraph.Length--;
            paragraph.Append(line);
            return;
        }

        paragraph.Append(' ').Append(line);
    }
}
