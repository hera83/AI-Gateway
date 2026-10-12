using api.Managers.KnowledgeBase.Dtos;
using api.Managers.KnowledgeBase.Interfaces;
using api.Repositories.KnowledgeDocuments.Dtos;
using api.Repositories.KnowledgeDocuments.Interfaces;
using api.Repositories.KnowledgeGroups.Interfaces;
using api.Services.Ollama.Dtos.Chat;
using api.Services.Ollama.Dtos.Common;
using api.Services.Ollama.Dtos.Embed;
using api.Services.Ollama.Interfaces;
using Microsoft.Extensions.Options;

namespace api.Managers.KnowledgeBase;

public class KnowledgeBaseManager(
    IKnowledgeGroupRepository knowledgeGroupRepository,
    IKnowledgeDocumentRepository knowledgeDocumentRepository,
    IOllamaService ollamaService,
    IOptions<KnowledgeBaseSettings> options) : IKnowledgeBaseManager
{
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

        if (!DocumentTextExtractor.IsSupported(dto.FileName))
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

        string text;
        try
        {
            await using var fileStream = await knowledgeDocumentRepository.OpenFileAsync(dto.ApiKeyId, document.Id, cancellationToken)
                ?? throw new InvalidOperationException("The stored file could not be opened.");
            text = await DocumentTextExtractor.ExtractTextAsync(fileStream, dto.FileName, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("No extractable text was found in the file.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failed = await knowledgeDocumentRepository.MarkFailedAsync(document.Id, exception.Message, cancellationToken);
            return new UploadDocumentResultDto { Outcome = UploadDocumentOutcome.Uploaded, Document = failed ?? document };
        }

        var chunks = ChunkText(text, settings.ChunkSizeCharacters, settings.ChunkOverlapCharacters);
        var embeddings = await ollamaService.EmbedAsync(new EmbedRequestDto { Model = settings.EmbeddingModel, Input = chunks }, cancellationToken);

        var indexed = await knowledgeDocumentRepository.MarkIndexedAsync(document.Id, new IndexKnowledgeDocumentDto
        {
            ExtractedText = text,
            Chunks = chunks.Select((chunk, index) => new CreateKnowledgeChunkDto { Text = chunk, Embedding = embeddings.Embeddings[index] }).ToList()
        }, cancellationToken);

        return new UploadDocumentResultDto { Outcome = UploadDocumentOutcome.Uploaded, Document = indexed ?? document };
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

    // Always spans every group/document the calling key owns, never just one group.
    public async Task<List<KnowledgeChunkMatchDto>> SearchAsync(Guid apiKeyId, string query, int? topK, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var embeddings = await ollamaService.EmbedAsync(new EmbedRequestDto { Model = settings.EmbeddingModel, Input = [query] }, cancellationToken);
        return await knowledgeDocumentRepository.SearchAsync(apiKeyId, embeddings.Embeddings[0], topK is > 0 ? topK.Value : settings.DefaultTopK, cancellationToken);
    }

    public async Task<RagChatResultDto> RagChatAsync(RagChatDto dto, CancellationToken cancellationToken = default)
    {
        // RagChatKnowledgeBaseRequestDto validates that a user message exists, so this is unexpected here.
        var lastUserMessage = dto.Messages.LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Messages must include at least one message with role 'user'.");

        // Retrieval uses only the latest user message, so stale context from earlier turns can't leak in.
        var matches = await SearchAsync(dto.ApiKeyId, lastUserMessage.Content, dto.TopK, cancellationToken);

        var systemPrompt = matches.Count > 0
            ? "Answer the user's question using only the following context. Cite sources by their [n] number." +
              Environment.NewLine + Environment.NewLine +
              string.Join(Environment.NewLine + Environment.NewLine, matches.Select((match, index) => $"[{index + 1}] {match.Text}"))
            : "No relevant context was found in the knowledge base. Answer based on general knowledge and say so.";

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
