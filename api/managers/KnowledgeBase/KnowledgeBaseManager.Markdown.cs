using System.Text.RegularExpressions;
using api.Managers.KnowledgeBase.Dtos;
using api.Services.Ollama;
using api.Services.Ollama.Dtos.Chat;
using api.Services.Ollama.Dtos.Common;

namespace api.Managers.KnowledgeBase;

// LLM layout of Markdown for documents that are mostly OCR text (scans, images), which has no structure to build
// on. Runs after indexing, in KnowledgeMarkdownWorker, so it never delays search. The input is the Markdown made
// from the structure (already without repeated page headers/footers, with labels and fields marked). The model may
// only change the layout: every part is checked word for word against its input, and a part that fails keeps the
// structure's Markdown. Progress is saved after every part, so a restart continues where it stopped.
public partial class KnowledgeBaseManager
{
    // Room for a part of LlmChunkCharacters, the instructions and the answer.
    private const int LlmContextTokens = 8192;

    private const string LlmMarkdownInstructions =
        """
        You improve the layout of Markdown that was made from OCR text (scanned pages). In the input, **Label** lines are titles or labels, "**Label:** value" lines are fields, and a backslash marks a character that is shown as written.
        Rules:
        - Keep every word exactly as written, in the same order and the same language. Never translate, summarize, shorten, explain, correct or add words.
        - Only change the layout: join lines that belong to the same paragraph, make headings, bulleted and numbered lists, and Markdown tables where the text is clearly a table.
        - Headings: ## for the main parts or entries of the document (for example each letter, note, report or chapter), ### for the sections inside them. Never use #### or deeper.
        - Short lines that name a section, often numbered like "1. Budget", are headings, not list items. Lines with details such as a date, a place or a name are normal text, unless they start a new entry.
        - Keep "**Label:** value" fields as fields; several in a row may become a list of them.
        - Leave nothing out. Every line of the input must be in your answer, also lines that repeat earlier lines, look like a header, footer, URL, code or OCR noise, or seem unimportant.
        - Answer with the Markdown only: no code fences around it, no comments before or after.
        """;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex ThinkingRegex();

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex ParagraphBreakRegex();

    public async Task<bool> ImproveNextMarkdownAsync(CancellationToken cancellationToken = default)
    {
        var document = await knowledgeDocumentRepository.GetNextMarkdownLlmPendingAsync(MarkdownFormatter.Version, cancellationToken);
        if (document is null)
        {
            return false;
        }

        var settings = options.Value.Markdown;
        if (string.IsNullOrWhiteSpace(settings.LlmModel) || string.IsNullOrWhiteSpace(document.MarkdownText))
        {
            await knowledgeDocumentRepository.SkipLlmMarkdownAsync(document.Id, cancellationToken);
            return true;
        }

        // The split only depends on the structure's Markdown and LlmChunkCharacters, so an earlier run with the same
        // number of parts is continued; otherwise it starts over.
        var parts = SplitForLlm(document.MarkdownText, settings.LlmChunkCharacters);
        var resume = document.PartsDone is { } partsDone && document.PartsTotal == parts.Count && partsDone <= parts.Count && document.Draft is not null;
        var start = resume ? document.PartsDone!.Value : 0;
        var draft = resume ? document.Draft! : string.Empty;
        var accepted = 0;
        if (resume)
        {
            logger.LogInformation("LLM Markdown for knowledge document {DocumentId} continues at part {Part} of {PartCount}", document.Id, start + 1, parts.Count);
        }

        for (var index = start; index < parts.Count; index++)
        {
            (string Markdown, int Accepted) part;
            try
            {
                part = await LayOutPartAsync(parts[index], index == 0, $"{index + 1}", document.Id, settings, canSplit: true, cancellationToken);
            }
            catch (OllamaException exception)
            {
                // The model is missing or rejects the request, so trying again won't help. An unreachable Ollama
                // (HttpRequestException) is not caught: the document stays pending and is tried again later.
                logger.LogWarning(exception, "LLM Markdown for knowledge document {DocumentId} skipped: Ollama answered {StatusCode}", document.Id, exception.StatusCode);
                await knowledgeDocumentRepository.SkipLlmMarkdownAsync(document.Id, cancellationToken);
                return true;
            }

            accepted += part.Accepted;
            draft = draft.Length == 0 ? part.Markdown : draft + "\n\n" + part.Markdown;

            // false = the document was deleted meanwhile.
            if (!await knowledgeDocumentRepository.SaveLlmMarkdownProgressAsync(document.Id, draft, index + 1, parts.Count, cancellationToken))
            {
                return true;
            }
        }

        logger.LogInformation(
            "LLM Markdown for knowledge document {DocumentId} with {Model}: {PartCount} parts in this run, {AcceptedCount} pieces laid out by the LLM (a part retried in halves counts as two)",
            document.Id, settings.LlmModel, parts.Count - start, accepted);

        // Nothing accepted in any run: the draft is the structure's Markdown again.
        if (draft == string.Join("\n\n", parts))
        {
            await knowledgeDocumentRepository.SkipLlmMarkdownAsync(document.Id, cancellationToken);
        }
        else
        {
            // Indexed again from the LLM's Markdown first: if that fails (Ollama gone), the document stays pending
            // with all parts done and only this last step is repeated.
            await ReindexAsync(document.Id, draft, cancellationToken);
            await knowledgeDocumentRepository.SetLlmMarkdownAsync(document.Id, draft, cancellationToken);
        }

        return true;
    }

    // The LLM's Markdown when it keeps the words, else the structure's. A rejected part is tried once more in two
    // halves: on a smaller part the model is less tempted to drop lines that repeat or look unimportant.
    // Accepted counts the pieces the LLM laid out.
    private async Task<(string Markdown, int Accepted)> LayOutPartAsync(
        string part,
        bool isStart,
        string partLabel,
        Guid documentId,
        KnowledgeBaseMarkdownSettings settings,
        bool canSplit,
        CancellationToken cancellationToken)
    {
        string markdown;
        try
        {
            markdown = await LayOutWithLlmAsync(part, isStart, settings.LlmModel, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("LLM Markdown for part {Part} of knowledge document {DocumentId} timed out", partLabel, documentId);
            return (part, 0);
        }

        if (KeepsWords(part, markdown, settings.LlmMinWordCoverage, out var coverage, out var added))
        {
            return (markdown, 1);
        }

        var halves = canSplit ? SplitInHalf(part) : null;
        logger.LogInformation(
            "LLM Markdown for part {Part} of knowledge document {DocumentId} rejected: kept {Coverage:P0} of the words, added {Added:P0}{Retry}",
            partLabel, documentId, coverage, added, halves is null ? string.Empty : "; trying again in two halves");

        if (halves is null)
        {
            return (part, 0);
        }

        var first = await LayOutPartAsync(halves.Value.First, isStart, partLabel + "a", documentId, settings, canSplit: false, cancellationToken);
        var second = await LayOutPartAsync(halves.Value.Second, false, partLabel + "b", documentId, settings, canSplit: false, cancellationToken);
        return (first.Markdown + "\n\n" + second.Markdown, first.Accepted + second.Accepted);
    }

    // At the paragraph break nearest the middle. null for a single paragraph.
    private static (string First, string Second)? SplitInHalf(string part)
    {
        var paragraphs = part.Split("\n\n");
        if (paragraphs.Length < 2)
        {
            return null;
        }

        var length = 0;
        var splitAt = 1;
        for (var index = 0; index < paragraphs.Length - 1; index++)
        {
            length += paragraphs[index].Length + 2;
            splitAt = index + 1;
            if (length >= part.Length / 2)
            {
                break;
            }
        }

        return (string.Join("\n\n", paragraphs[..splitAt]), string.Join("\n\n", paragraphs[splitAt..]));
    }

    private bool NeedsLlmMarkdown(ExtractedDocumentDto extracted)
    {
        var settings = options.Value.Markdown;
        return !string.IsNullOrWhiteSpace(settings.LlmModel) && extracted.OcrShare >= settings.LlmMinOcrShare;
    }

    // Each part is laid out on its own, so only the first one is told it holds the document's title.
    private async Task<string> LayOutWithLlmAsync(string text, bool isStart, string model, CancellationToken cancellationToken)
    {
        var position = isStart
            ? "This is the start of the document. If it begins with the document's title, make that a # heading."
            : "This continues a document. Do not use # headings.";

        var chat = await ollamaService.ChatAsync(new ChatRequestDto
        {
            Model = model,
            Messages =
            [
                new MessageDto { Role = "system", Content = LlmMarkdownInstructions + "\n" + position },
                new MessageDto { Role = "user", Content = text }
            ],
            Options = new OptionsDto { Temperature = 0, NumCtx = LlmContextTokens },
            Stream = false
        }, cancellationToken);

        // Some models think out loud first, or wrap the answer in a code fence despite the instructions.
        var markdown = ThinkingRegex().Replace(MarkdownFormatter.NormalizeNewLines(chat.Message.Content), string.Empty).Trim();
        if (markdown.StartsWith("```", StringComparison.Ordinal) && markdown.EndsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = markdown.IndexOf('\n');
            markdown = firstLineEnd < 0 ? string.Empty : markdown[(firstLineEnd + 1)..^3].Trim();
        }

        return markdown;
    }

    // The LLM may only add layout. Compares the words (letters and digits, ignoring case and Markdown symbols)
    // of the source and the answer as multisets: enough kept, and not too many new ones.
    private static bool KeepsWords(string source, string markdown, double minCoverage, out double coverage, out double added)
    {
        var sourceWords = CountWords(source);
        var markdownWords = CountWords(markdown);
        var sourceCount = sourceWords.Values.Sum();
        if (sourceCount == 0)
        {
            coverage = added = 0;
            return false;
        }

        var common = sourceWords.Sum(word => Math.Min(word.Value, markdownWords.GetValueOrDefault(word.Key)));
        coverage = (double)common / sourceCount;
        added = (double)(markdownWords.Values.Sum() - common) / sourceCount;
        return coverage >= minCoverage && added <= 1 - minCoverage;
    }

    private static Dictionary<string, int> CountWords(string text) =>
        WordRegex().Matches(text.ToLowerInvariant())
            .GroupBy(match => match.Value)
            .ToDictionary(group => group.Key, group => group.Count());

    // Whole paragraphs up to about maxCharacters. A longer paragraph is split at its lines.
    private static List<string> SplitForLlm(string text, int maxCharacters)
    {
        var parts = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        void Flush()
        {
            if (current.Count > 0)
            {
                parts.Add(string.Join("\n\n", current));
            }

            current.Clear();
            currentLength = 0;
        }

        var pieces = ParagraphBreakRegex().Split(MarkdownFormatter.NormalizeNewLines(text).Trim())
            .SelectMany(paragraph => paragraph.Length <= maxCharacters ? [paragraph] : paragraph.Split('\n'))
            .Where(piece => piece.Trim().Length > 0);

        foreach (var piece in pieces)
        {
            if (currentLength > 0 && currentLength + piece.Length > maxCharacters)
            {
                Flush();
            }

            current.Add(piece);
            currentLength += piece.Length + 2;
        }

        Flush();
        return parts;
    }
}
