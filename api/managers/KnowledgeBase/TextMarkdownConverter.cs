using System.Text;

namespace api.Managers.KnowledgeBase;

// Plain-text uploads as Markdown: Markdown files as they are, CSV/TSV as a table, structured and source files as
// a code block, and everything else as paragraphs and lists.
public static class TextMarkdownConverter
{
    private static readonly string[] MarkdownExtensions = [".md", ".markdown", ".mdown", ".mkd"];

    // The extension decides the code block's language, so it gets syntax highlighting.
    private static readonly Dictionary<string, string> CodeLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        [".json"] = "json", [".xml"] = "xml", [".svg"] = "xml", [".config"] = "xml", [".csproj"] = "xml", [".props"] = "xml",
        [".targets"] = "xml", [".yaml"] = "yaml", [".yml"] = "yaml", [".toml"] = "toml", [".ini"] = "ini", [".cfg"] = "ini",
        [".conf"] = "ini", [".html"] = "html", [".htm"] = "html", [".css"] = "css", [".js"] = "javascript", [".mjs"] = "javascript",
        [".ts"] = "typescript", [".tsx"] = "tsx", [".jsx"] = "jsx", [".cs"] = "csharp", [".vb"] = "vbnet", [".py"] = "python",
        [".java"] = "java", [".kt"] = "kotlin", [".go"] = "go", [".rs"] = "rust", [".c"] = "c", [".h"] = "c", [".cpp"] = "cpp",
        [".cc"] = "cpp", [".hpp"] = "cpp", [".swift"] = "swift", [".php"] = "php", [".rb"] = "ruby", [".sql"] = "sql",
        [".sh"] = "bash", [".bash"] = "bash", [".ps1"] = "powershell", [".bat"] = "bat", [".cmd"] = "bat",
        [".graphql"] = "graphql", [".log"] = ""
    };

    public static string Convert(string text, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        text = MarkdownFormatter.NormalizeNewLines(text);

        if (MarkdownExtensions.Contains(extension))
        {
            return text.Trim('\n');
        }

        if (extension is ".csv" or ".tsv" or ".tab" && ToTable(text, extension) is { } table)
        {
            return table;
        }

        return CodeLanguages.TryGetValue(extension, out var language)
            ? MarkdownFormatter.CodeBlock(text, language)
            : MarkdownFormatter.FromPlainText(text);
    }

    // null when the file doesn't parse into at least two columns, so it falls back to plain text.
    private static string? ToTable(string text, string extension)
    {
        var delimiter = extension == ".csv" ? DetectDelimiter(text) : '\t';
        var rows = ParseDelimited(text, delimiter);
        if (rows.Count == 0 || rows.Max(row => row.Count) < 2)
        {
            return null;
        }

        return MarkdownFormatter.Table(rows.Select(row => (IReadOnlyList<string>)row.Select(MarkdownFormatter.Escape).ToList()).ToList());
    }

    // The most common of ; , and tab in the first line. Danish CSV files usually use ';' (',' is the decimal mark).
    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        return new[] { ';', ',', '\t' }.OrderByDescending(candidate => firstLine.Count(character => character == candidate)).First();
    }

    // RFC 4180: quoted fields may hold the delimiter, line breaks and doubled quotes.
    private static List<List<string>> ParseDelimited(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (inQuotes)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(character);
                }
            }
            else if (character == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (character == delimiter)
            {
                row.Add(field.ToString().Trim());
                field.Clear();
            }
            else if (character == '\n')
            {
                row.Add(field.ToString().Trim());
                field.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString().Trim());
            rows.Add(row);
        }

        return rows.Where(parsed => parsed.Any(cell => cell.Length > 0)).ToList();
    }
}
