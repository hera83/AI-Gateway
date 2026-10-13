using System.Text.RegularExpressions;

namespace api.Managers.KnowledgeBase;

// Splits a document's Markdown into chunks for embedding along its structure instead of every N characters:
// a chunk is whole paragraphs, lists and tables under one section, and starts with the headings above it
// ("## Note of 27.08" / "### Plan"), so both the vector search and the chat model know where the text comes from.
// The Markdown has no repeated page headers/footers, which would otherwise make every chunk of a scan look alike.
public static partial class MarkdownChunker
{
    // Bump when the chunking changes: documents indexed with an older version are indexed again in the background
    // from their current Markdown (KnowledgeBaseManager.ProcessNextDocumentAsync). 0 = chunks of the raw text.
    public const int Version = 1;

    // # and ## always start a new chunk (a document's parts or entries); deeper headings only once the chunk is at
    // least this full, so short sections ("### Plan" with one line) stay with their neighbours.
    private const int AlwaysSplitHeadingLevel = 2;
    private const double MinFillBeforeSubheadingSplit = 0.5;

    [GeneratedRegex(@"^(?<marks>#{1,6})\s+\S")]
    private static partial Regex HeadingRegex();

    // A bold line on its own ("**Plan**") is a label in Markdown made from OCR text, which has no real headings.
    // It counts as the deepest heading level, so chunks still say which section they belong to.
    [GeneratedRegex(@"^\*\*[^*\n]+\*\*$")]
    private static partial Regex BoldLabelRegex();

    private const int LabelLevel = 6;

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex BlankLinesRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBreakRegex();

    public static List<string> Chunk(string markdown, int maxCharacters, int overlapCharacters)
    {
        var headings = new string?[6];
        var chunks = new List<string>();
        var body = new List<string>();
        var bodyLength = 0;
        var prefix = string.Empty;

        void Flush()
        {
            if (body.Count > 0)
            {
                chunks.Add(prefix + string.Join("\n\n", body));
            }

            body.Clear();
            bodyLength = 0;
        }

        void Add(string block)
        {
            // A chunk starts with the headings it sits under; headings that come later stay inside its text.
            if (body.Count == 0)
            {
                var path = headings.Where(heading => heading is not null).ToList();
                prefix = path.Count == 0 ? string.Empty : string.Join("\n", path) + "\n\n";
            }

            body.Add(block);
            bodyLength += block.Length + 2;
        }

        foreach (var block in SplitBlocks(markdown).SelectMany(SplitHeadingFromText))
        {
            var heading = HeadingRegex().Match(block);
            if (heading.Success || BoldLabelRegex().IsMatch(block))
            {
                var level = heading.Success ? heading.Groups["marks"].Length : LabelLevel;
                if (level <= AlwaysSplitHeadingLevel || bodyLength >= maxCharacters * MinFillBeforeSubheadingSplit)
                {
                    Flush();
                }

                headings[level - 1] = block.Split('\n')[0];
                Array.Fill(headings, null, level, headings.Length - level);
                if (body.Count > 0)
                {
                    Add(block);
                }

                continue;
            }

            if (block.Length > maxCharacters)
            {
                Flush();
                foreach (var piece in SplitLongBlock(block, maxCharacters, overlapCharacters))
                {
                    Add(piece);
                    Flush();
                }

                continue;
            }

            if (bodyLength > 0 && bodyLength + block.Length > maxCharacters)
            {
                Flush();
            }

            Add(block);
        }

        Flush();
        return chunks;
    }

    // Blocks are separated by blank lines, except inside a fenced code block, which stays one block.
    private static IEnumerable<string> SplitBlocks(string markdown)
    {
        string? openFence = null;
        foreach (var block in BlankLinesRegex().Split(MarkdownFormatter.NormalizeNewLines(markdown).Trim()).Where(block => block.Trim().Length > 0))
        {
            if (openFence is not null)
            {
                openFence += "\n\n" + block;
                if (block.TrimEnd().EndsWith("```", StringComparison.Ordinal))
                {
                    yield return openFence;
                    openFence = null;
                }

                continue;
            }

            var trimmed = block.TrimStart();
            var isUnclosedFence = trimmed.StartsWith("```", StringComparison.Ordinal) &&
                                  (trimmed.Split('\n').Length == 1 || !trimmed.TrimEnd().EndsWith("```", StringComparison.Ordinal));
            if (isUnclosedFence)
            {
                openFence = block;
                continue;
            }

            yield return block;
        }

        if (openFence is not null)
        {
            yield return openFence;
        }
    }

    // "## Heading\ntext" without a blank line between: the heading and its text are two blocks.
    private static IEnumerable<string> SplitHeadingFromText(string block)
    {
        var lineEnd = block.IndexOf('\n');
        if (lineEnd < 0 || !HeadingRegex().IsMatch(block))
        {
            return [block];
        }

        var rest = block[(lineEnd + 1)..].Trim();
        return rest.Length == 0 ? [block[..lineEnd]] : [block[..lineEnd], .. SplitHeadingFromText(rest)];
    }

    // A block longer than a chunk (a huge table, a wall of text) is split at lines, then sentences, then characters;
    // consecutive pieces overlap, so a sentence at a boundary is in both.
    private static List<string> SplitLongBlock(string block, int maxCharacters, int overlapCharacters)
    {
        var units = block.Split('\n')
            .SelectMany(line => line.Length <= maxCharacters ? [line] : SentenceBreakRegex().Split(line))
            .SelectMany(unit => unit.Length <= maxCharacters ? [unit] : unit.Chunk(maxCharacters).Select(characters => new string(characters)))
            .ToList();

        var pieces = new List<string>();
        var current = new List<string>();
        var length = 0;
        foreach (var unit in units)
        {
            if (length > 0 && length + unit.Length > maxCharacters)
            {
                pieces.Add(string.Join("\n", current));

                // Carry the last units over, up to overlapCharacters.
                var carried = new List<string>();
                var carriedLength = 0;
                for (var index = current.Count - 1; index >= 0 && carriedLength + current[index].Length <= overlapCharacters; index--)
                {
                    carried.Insert(0, current[index]);
                    carriedLength += current[index].Length + 1;
                }

                current = carried;
                length = carriedLength;
            }

            current.Add(unit);
            length += unit.Length + 1;
        }

        if (current.Count > 0)
        {
            pieces.Add(string.Join("\n", current));
        }

        return pieces;
    }
}
