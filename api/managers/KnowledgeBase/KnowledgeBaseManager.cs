using api.Managers.KnowledgeBase.Dtos;
using api.Managers.KnowledgeBase.Interfaces;
using api.Repositories.KnowledgeDocuments.Dtos;
using api.Repositories.KnowledgeDocuments.Interfaces;
using api.Repositories.KnowledgeGroups.Interfaces;
using api.Services.Ollama.Dtos.Chat;
using api.Services.Ollama.Dtos.Common;
using api.Services.Ollama.Dtos.Embed;
using api.Services.Ollama.Interfaces;
using api.Services.Tesseract.Interfaces;
using Microsoft.Extensions.Options;

namespace api.Managers.KnowledgeBase;

// Text extraction (incl. OCR) lives in KnowledgeBaseManager.Extraction.cs.
public partial class KnowledgeBaseManager(
    IKnowledgeGroupRepository knowledgeGroupRepository,
    IKnowledgeDocumentRepository knowledgeDocumentRepository,
    IOllamaService ollamaService,
    ITesseractService tesseractService,
    KnowledgeDocumentProcessingSignal processingSignal,
    IOptions<KnowledgeBaseSettings> options,
    ILogger<KnowledgeBaseManager> logger) : IKnowledgeBaseManager
{
    private const string RagContextInstructions =
        """
        Answer the user's question using only the context below, in the same language as the question.
        Each part of the context starts with its number and source file; its text may start with the headings of the section it comes from, which tell you what the text is about (for example which note or date it belongs to).
        Cite the parts you use by their number, like [1] or [2][3].
        If the context does not contain the answer, say so plainly instead of guessing.
        """;

    // Only stores the file. Extraction, embedding and indexing happen in KnowledgeDocumentProcessingWorker
    // (ProcessNextDocumentAsync), so the returned document has Status "Processing".
    public async Task<UploadDocumentResultDto> UploadDocumentAsync(UploadDocumentDto dto, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (await knowledgeGroupRepository.GetByIdAsync(dto.ApiKeyId, dto.KnowledgeGroupId, cancellationToken) is null)
        {
            return new UploadDocumentResultDto { Outcome = UploadDocumentOutcome.GroupNotFound };
        }

        if (dto.SizeBytes > settings.MaxUploadSizeBytes)
        {
            return new UploadDocumentResultDto { Outcome = UploadDocumentOutcome.TooLarge };
        }

        if (await DocumentTextExtractor.DetectKindAsync(dto.Content, dto.FileName, cancellationToken) == DocumentKind.Unsupported)
        {
            return new UploadDocumentResultDto { Outcome = UploadDocumentOutcome.UnsupportedFileType };
        }

        // Stored before extraction, so the original upload stays downloadable even if indexing fails.
        var document = await knowledgeDocumentRepository.CreateAsync(new CreateKnowledgeDocumentDto
        {
            ApiKeyId = dto.ApiKeyId,
            KnowledgeGroupId = dto.KnowledgeGroupId,
            OriginalFileName = dto.FileName,
            ContentType = dto.ContentType,
            SizeBytes = dto.SizeBytes,
            Content = dto.Content
        }, cancellationToken);

        processingSignal.Notify();
        return new UploadDocumentResultDto { Outcome = UploadDocumentOutcome.Uploaded, Document = document };
    }

    public async Task<bool> ProcessNextDocumentAsync(CancellationToken cancellationToken = default)
    {
        if (await knowledgeDocumentRepository.GetNextProcessingAsync(cancellationToken) is { } document)
        {
            await ProcessDocumentAsync(document, cancellationToken);
            return true;
        }

        // New uploads first. Then documents whose Markdown is missing or made by an older converter version get it
        // again, one at a time.
        if (await knowledgeDocumentRepository.GetNextWithOutdatedMarkdownAsync(MarkdownFormatter.Version, cancellationToken) is { } outdated)
        {
            await RemakeMarkdownAsync(outdated, cancellationToken);
            return true;
        }

        // Last, documents whose chunks were made by an older chunker version are indexed again from their Markdown.
        if (await knowledgeDocumentRepository.GetNextWithOutdatedIndexAsync(MarkdownChunker.Version, cancellationToken) is { } outdatedIndex)
        {
            await ReindexOutdatedAsync(outdatedIndex, cancellationToken);
            return true;
        }

        return false;
    }

    // Only the Markdown is stored; the indexed text, chunks and vectors are left as they are. If the file can't be
    // read again, the stored text is used as Markdown, so the document is never retried.
    private async Task RemakeMarkdownAsync(KnowledgeDocumentDto document, CancellationToken cancellationToken)
    {
        string markdown;
        var needsLlm = false;
        try
        {
            var extracted = await ExtractStoredDocumentAsync(document, cancellationToken);
            markdown = extracted.Markdown;
            needsLlm = NeedsLlmMarkdown(extracted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Markdown for knowledge document {DocumentId} fell back to its stored text", document.Id);
            var stored = await knowledgeDocumentRepository.GetTextAsync(document.ApiKeyId, document.Id, cancellationToken);
            markdown = MarkdownFormatter.FromPlainText(stored?.ExtractedText ?? string.Empty);
        }

        if (await knowledgeDocumentRepository.SetMarkdownAsync(document.Id, markdown, needsLlm, MarkdownFormatter.Version, cancellationToken))
        {
            logger.LogInformation("Made Markdown version {Version} for knowledge document {DocumentId}", MarkdownFormatter.Version, document.Id);

            // The index follows the Markdown. If this fails, the document's IndexVersion stays old and it's tried again.
            await ReindexAsync(document.Id, markdown, cancellationToken);
        }
    }

    // New chunks from the document's current Markdown, embedded and swapped in for the old ones. Only embeddings; the
    // file isn't read again.
    private async Task ReindexAsync(Guid documentId, string markdown, CancellationToken cancellationToken)
    {
        var chunks = await EmbedChunksAsync(ChunkForIndex(markdown, null), cancellationToken);
        if (chunks.Count > 0 && await knowledgeDocumentRepository.ReplaceChunksAsync(documentId, chunks, MarkdownChunker.Version, cancellationToken))
        {
            logger.LogInformation("Indexed knowledge document {DocumentId} again from its Markdown: {ChunkCount} chunks", documentId, chunks.Count);
        }
    }

    // Documents indexed with an older chunker version (or from the raw text) are indexed again from their Markdown.
    // The version is always updated, also when there is nothing to chunk, so the worker never picks it again.
    private async Task ReindexOutdatedAsync(KnowledgeDocumentDto document, CancellationToken cancellationToken)
    {
        var markdown = (await knowledgeDocumentRepository.GetMarkdownAsync(document.ApiKeyId, document.Id, cancellationToken))?.MarkdownText ?? string.Empty;
        var fallbackText = string.IsNullOrWhiteSpace(markdown)
            ? (await knowledgeDocumentRepository.GetTextAsync(document.ApiKeyId, document.Id, cancellationToken))?.ExtractedText
            : null;

        var chunks = await EmbedChunksAsync(ChunkForIndex(markdown, fallbackText), cancellationToken);
        if (await knowledgeDocumentRepository.ReplaceChunksAsync(document.Id, chunks, MarkdownChunker.Version, cancellationToken))
        {
            logger.LogInformation("Indexed knowledge document {DocumentId} again from its Markdown: {ChunkCount} chunks", document.Id, chunks.Count);
        }
    }

    // Chunks along the Markdown's structure (see MarkdownChunker). Plain text is only the fallback for a document
    // whose Markdown came out empty.
    private List<string> ChunkForIndex(string markdown, string? fallbackText)
    {
        var settings = options.Value;
        if (!string.IsNullOrWhiteSpace(markdown))
        {
            return MarkdownChunker.Chunk(markdown, settings.ChunkSizeCharacters, settings.ChunkOverlapCharacters);
        }

        return string.IsNullOrWhiteSpace(fallbackText) ? [] : ChunkText(fallbackText, settings.ChunkSizeCharacters, settings.ChunkOverlapCharacters);
    }

    private async Task<List<CreateKnowledgeChunkDto>> EmbedChunksAsync(List<string> chunks, CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return [];
        }

        var embeddings = await ollamaService.EmbedAsync(new EmbedRequestDto { Model = options.Value.EmbeddingModel, Input = chunks }, cancellationToken);
        return chunks.Select((chunk, index) => new CreateKnowledgeChunkDto { Text = chunk, Embedding = embeddings.Embeddings[index] }).ToList();
    }

    private async Task<ExtractedDocumentDto> ExtractStoredDocumentAsync(KnowledgeDocumentDto document, CancellationToken cancellationToken)
    {
        await using var fileStream = await knowledgeDocumentRepository.OpenFileAsync(document.ApiKeyId, document.Id, cancellationToken)
            ?? throw new InvalidOperationException("The stored file could not be opened.");
        return await ExtractDocumentAsync(fileStream, document.OriginalFileName, document.Id, cancellationToken);
    }

    // Extraction, embedding (Ollama) and indexing all fail the same way: the document is kept with Status
    // "Failed" and the reason, so it never stays "Processing". HttpClient timeouts are also
    // OperationCanceledException, so only the worker's own cancellation (shutdown) is let through. The document
    // then stays "Processing" and is picked up again on the next start.
    private async Task ProcessDocumentAsync(KnowledgeDocumentDto document, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        try
        {
            var extracted = await ExtractStoredDocumentAsync(document, cancellationToken);
            var text = extracted.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("No extractable text was found in the file.");
            }

            var chunks = await EmbedChunksAsync(ChunkForIndex(extracted.Markdown, text), cancellationToken);

            // null = the document was deleted while it was being processed.
            if (await knowledgeDocumentRepository.MarkIndexedAsync(document.Id, new IndexKnowledgeDocumentDto
                {
                    ExtractedText = text,
                    Markdown = extracted.Markdown,
                    MarkdownNeedsLlm = NeedsLlmMarkdown(extracted),
                    MarkdownVersion = MarkdownFormatter.Version,
                    IndexVersion = MarkdownChunker.Version,
                    Chunks = chunks
                }, cancellationToken) is not null)
            {
                logger.LogInformation("Indexed knowledge document {DocumentId}: {ChunkCount} chunks", document.Id, chunks.Count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Indexing knowledge document {DocumentId} failed", document.Id);

            // ErrorMessage is shown to the caller. A connection error's own message names Ollama's internal host and port.
            var reason = exception is HttpRequestException ? "The embedding service (Ollama) could not be reached." : exception.Message;
            await knowledgeDocumentRepository.MarkFailedAsync(document.Id, reason, cancellationToken);
        }
    }

    public async Task<bool> DeleteGroupAsync(Guid apiKeyId, Guid knowledgeGroupId, CancellationToken cancellationToken = default)
    {
        if (await knowledgeGroupRepository.GetByIdAsync(apiKeyId, knowledgeGroupId, cancellationToken) is null)
        {
            return false;
        }

        // Documents first, so their vectors and files are removed too (the group delete only cascades rows).
        foreach (var documentId in await knowledgeDocumentRepository.GetIdsAsync(apiKeyId, knowledgeGroupId, cancellationToken))
        {
            await knowledgeDocumentRepository.DeleteAsync(apiKeyId, documentId, cancellationToken);
        }

        return await knowledgeGroupRepository.DeleteAsync(apiKeyId, knowledgeGroupId, cancellationToken);
    }

    // Without knowledgeGroupId it spans every group/document the calling key owns. null = the group doesn't exist.
    public async Task<List<KnowledgeChunkMatchDto>?> SearchAsync(
        Guid apiKeyId, string query, int? topK, Guid? knowledgeGroupId, CancellationToken cancellationToken = default)
    {
        if (knowledgeGroupId is { } groupId && await knowledgeGroupRepository.GetByIdAsync(apiKeyId, groupId, cancellationToken) is null)
        {
            return null;
        }

        return await FindMatchesAsync(apiKeyId, query, topK, knowledgeGroupId, cancellationToken);
    }

    // Without KnowledgeGroupId retrieval spans every group the calling key owns. null = the group doesn't exist.
    public async Task<RagChatResultDto?> RagChatAsync(RagChatDto dto, CancellationToken cancellationToken = default)
    {
        // RagChatKnowledgeBaseRequestDto validates that a user message exists, so this is unexpected here.
        var lastUserMessage = dto.Messages.LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Messages must include at least one message with role 'user'.");

        // Retrieval uses only the latest user message, so stale context from earlier turns can't leak in.
        var matches = await SearchAsync(dto.ApiKeyId, lastUserMessage.Content, dto.TopK, dto.KnowledgeGroupId, cancellationToken);
        if (matches is null)
        {
            return null;
        }

        // Each part names its file, and its text starts with the headings of the section it comes from (MarkdownChunker),
        // so the model can tell parts of different documents and entries apart.
        var systemPrompt = matches.Count > 0
            ? RagContextInstructions + "\n\n" +
              string.Join("\n\n", matches.Select((match, index) => $"[{index + 1}] Source: {match.OriginalFileName}\n{match.Text}"))
            : "No relevant context was found in the knowledge base. Answer based on general knowledge, in the same language as the question, and say that the knowledge base had nothing on it.";

        // The fresh system message replaces any system message the caller sent; everything else is forwarded as-is.
        var messages = new List<MessageDto> { new() { Role = "system", Content = systemPrompt } };
        messages.AddRange(dto.Messages
            .Where(message => !string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
            .Select(message => new MessageDto { Role = message.Role, Content = message.Content }));

        var chat = await ollamaService.ChatAsync(new ChatRequestDto { Model = dto.Model, Messages = messages }, cancellationToken);

        return new RagChatResultDto
        {
            Model = chat.Model,
            CreatedAt = chat.CreatedAt,
            Message = new RagChatMessageDto { Role = chat.Message.Role, Content = chat.Message.Content },
            Done = chat.Done,
            DoneReason = chat.DoneReason,
            TotalDuration = chat.TotalDuration,
            LoadDuration = chat.LoadDuration,
            PromptEvalCount = chat.PromptEvalCount,
            PromptEvalDuration = chat.PromptEvalDuration,
            EvalCount = chat.EvalCount,
            EvalDuration = chat.EvalDuration,
            Sources = matches
        };
    }

    private async Task<List<KnowledgeChunkMatchDto>> FindMatchesAsync(
        Guid apiKeyId, string query, int? topK, Guid? knowledgeGroupId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var embeddings = await ollamaService.EmbedAsync(new EmbedRequestDto { Model = settings.EmbeddingModel, Input = [query] }, cancellationToken);
        return await knowledgeDocumentRepository.SearchAsync(
            apiKeyId, embeddings.Embeddings[0], topK is > 0 ? topK.Value : settings.DefaultTopK, knowledgeGroupId, cancellationToken);
    }

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var step = Math.Max(1, chunkSize - overlap);
        var chunks = new List<string>();

        for (var start = 0; start < text.Length; start += step)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            var chunk = text.Substring(start, length).Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (start + length >= text.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
