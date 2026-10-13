using System.Text.RegularExpressions;
using api.Managers.KnowledgeBase.Dtos;
using api.Repositories.KnowledgeDocuments.Dtos;
using api.Repositories.KnowledgeGroups.Dtos;

namespace api.Dtos.KnowledgeBase;

public static class KnowledgeBaseMapper
{
    public static CreateKnowledgeGroupDto ToCreateKnowledgeGroupDto(this CreateGroupKnowledgeBaseRequestDto request, Guid apiKeyId) => new()
    {
        ApiKeyId = apiKeyId,
        Name = request.Name
    };

    public static CreateGroupKnowledgeBaseResponseDto ToCreateGroupKnowledgeBaseResponseDto(this KnowledgeGroupDto group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        CreatedAt = group.CreatedAt
    };

    public static ListGroupsKnowledgeBaseResponseDto ToListGroupsKnowledgeBaseResponseDto(this KnowledgeGroupDto group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        CreatedAt = group.CreatedAt
    };

    public static UploadDocumentDto ToUploadDocumentDto(this UploadDocumentKnowledgeBaseRequestDto request, Guid apiKeyId) => new()
    {
        ApiKeyId = apiKeyId,
        KnowledgeGroupId = request.GroupId,
        FileName = request.File.FileName,
        ContentType = request.File.ContentType,
        SizeBytes = request.File.Length,
        Content = request.File.OpenReadStream()
    };

    public static UploadDocumentKnowledgeBaseResponseDto ToUploadDocumentKnowledgeBaseResponseDto(this KnowledgeDocumentDto document) => new()
    {
        Id = document.Id,
        GroupId = document.KnowledgeGroupId,
        FileName = document.OriginalFileName,
        ContentType = document.ContentType,
        SizeBytes = document.SizeBytes,
        Status = document.Status,
        ErrorMessage = document.ErrorMessage,
        UploadedAt = document.CreatedAt
    };

    public static ListDocumentsKnowledgeBaseResponseDto ToListDocumentsKnowledgeBaseResponseDto(this KnowledgeDocumentDto document) => new()
    {
        Id = document.Id,
        GroupId = document.KnowledgeGroupId,
        FileName = document.OriginalFileName,
        ContentType = document.ContentType,
        SizeBytes = document.SizeBytes,
        Status = document.Status,
        ErrorMessage = document.ErrorMessage,
        UploadedAt = document.CreatedAt
    };

    public static GetDocumentTextKnowledgeBaseResponseDto ToGetDocumentTextKnowledgeBaseResponseDto(this KnowledgeDocumentTextDto document) => new()
    {
        Id = document.Id,
        GroupId = document.KnowledgeGroupId,
        FileName = document.OriginalFileName,
        Status = document.Status,
        ErrorMessage = document.ErrorMessage,
        Text = document.ExtractedText
    };

    // "Journal.pdf" → "Journal.md".
    public static string ToMarkdownFileName(this KnowledgeDocumentMarkdownDto document) =>
        Path.GetFileNameWithoutExtension(document.OriginalFileName) + ".md";

    // "12/50" while the LLM lays out the Markdown, otherwise null.
    public static string? ToLlmProgress(this KnowledgeDocumentMarkdownDto document) =>
        document.LlmPartsDone is { } done && document.LlmPartsTotal is { } total ? $"{done}/{total}" : null;

    public static SearchKnowledgeBaseResponseDto ToSearchKnowledgeBaseResponseDto(this KnowledgeChunkMatchDto match) => new()
    {
        ChunkId = match.ChunkId,
        DocumentId = match.KnowledgeDocumentId,
        DocumentFileName = match.OriginalFileName,
        GroupId = match.KnowledgeGroupId,
        Text = match.Text,
        Score = match.Score
    };

    public static AgentChatDto ToAgentChatDto(this AgentChatKnowledgeBaseRequestDto request, Guid apiKeyId) => new()
    {
        ApiKeyId = apiKeyId,
        Model = request.Model,
        Messages = request.Messages.Select(message => new RagChatMessageDto { Role = message.Role, Content = message.Content }).ToList(),
        KnowledgeGroupId = request.GroupId,
        SkipClarification = request.SkipClarification
    };

    public static AgentChatKnowledgeBaseResponseDto ToAgentChatKnowledgeBaseResponseDto(this AgentChatResultDto result)
    {
        // "[2]", "[2][5]" and "[2, 5]" all cite.
        var cited = Regex.Matches(result.Message, @"\[(\d+(?:\s*[,;]\s*\d+)*)\]")
            .SelectMany(match => match.Groups[1].Value.Split([',', ';']))
            .Select(number => int.TryParse(number.Trim(), out var value) ? value : 0)
            .ToHashSet();

        return new AgentChatKnowledgeBaseResponseDto
        {
            Status = result.Outcome.ToString(),
            Model = result.Model,
            Task = result.Task,
            Message = new ChatMessageDto { Role = "assistant", Content = result.Message },
            Questions = result.Questions,
            Queries = result.Queries,
            Sources = result.Sources.Select((source, index) => new AgentSourceDto
            {
                Number = index + 1,
                Cited = cited.Contains(index + 1),
                ChunkId = source.ChunkId,
                DocumentId = source.KnowledgeDocumentId,
                DocumentFileName = source.OriginalFileName,
                GroupId = source.KnowledgeGroupId,
                Text = source.Text,
                Score = source.Score
            }).ToList()
        };
    }

    public static RagChatDto ToRagChatDto(this RagChatKnowledgeBaseRequestDto request, Guid apiKeyId) => new()
    {
        ApiKeyId = apiKeyId,
        Model = request.Model,
        Messages = request.Messages.Select(message => new RagChatMessageDto { Role = message.Role, Content = message.Content }).ToList(),
        TopK = request.TopK,
        KnowledgeGroupId = request.GroupId
    };

    public static RagChatKnowledgeBaseResponseDto ToRagChatKnowledgeBaseResponseDto(this RagChatResultDto result) => new()
    {
        Model = result.Model,
        CreatedAt = result.CreatedAt,
        Message = new ChatMessageDto { Role = result.Message.Role, Content = result.Message.Content },
        Done = result.Done,
        DoneReason = result.DoneReason,
        TotalDuration = result.TotalDuration,
        LoadDuration = result.LoadDuration,
        PromptEvalCount = result.PromptEvalCount,
        PromptEvalDuration = result.PromptEvalDuration,
        EvalCount = result.EvalCount,
        EvalDuration = result.EvalDuration,
        Sources = result.Sources.Select(ToSourceDto).ToList()
    };

    public static SourceDto ToSourceDto(this KnowledgeChunkMatchDto match) => new()
    {
        ChunkId = match.ChunkId,
        DocumentId = match.KnowledgeDocumentId,
        DocumentFileName = match.OriginalFileName,
        GroupId = match.KnowledgeGroupId,
        Text = match.Text,
        Score = match.Score
    };
}
