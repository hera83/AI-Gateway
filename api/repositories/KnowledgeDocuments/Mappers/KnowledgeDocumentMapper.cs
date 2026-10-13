using System.Linq.Expressions;
using api.Data.Models;
using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Repositories.KnowledgeDocuments.Mappers;

public static class KnowledgeDocumentMapper
{
    public static readonly Expression<Func<KnowledgeDocument, KnowledgeDocumentDto>> ToDtoExpression = x => new KnowledgeDocumentDto
    {
        Id = x.Id,
        ApiKeyId = x.ApiKeyId,
        KnowledgeGroupId = x.KnowledgeGroupId,
        OriginalFileName = x.OriginalFileName,
        ContentType = x.ContentType,
        SizeBytes = x.SizeBytes,
        Status = x.Status.ToString(),
        ErrorMessage = x.ErrorMessage,
        CreatedAt = x.CreatedAt
    };

    public static readonly Expression<Func<KnowledgeDocument, KnowledgeDocumentTextDto>> ToTextDtoExpression = x => new KnowledgeDocumentTextDto
    {
        Id = x.Id,
        KnowledgeGroupId = x.KnowledgeGroupId,
        OriginalFileName = x.OriginalFileName,
        Status = x.Status.ToString(),
        ErrorMessage = x.ErrorMessage,
        ExtractedText = x.ExtractedText
    };

    public static readonly Expression<Func<KnowledgeDocument, KnowledgeDocumentMarkdownDto>> ToMarkdownDtoExpression = x => new KnowledgeDocumentMarkdownDto
    {
        Id = x.Id,
        KnowledgeGroupId = x.KnowledgeGroupId,
        OriginalFileName = x.OriginalFileName,
        Status = x.Status.ToString(),
        ErrorMessage = x.ErrorMessage,
        MarkdownText = x.MarkdownText,
        // Markdown made before sources were tracked was made from the structure.
        MarkdownSource = x.MarkdownText == null ? null : x.MarkdownSource == null ? nameof(KnowledgeMarkdownSource.Structure) : x.MarkdownSource.ToString(),
        LlmPartsDone = x.MarkdownLlmPartsDone,
        LlmPartsTotal = x.MarkdownLlmPartsTotal
    };

    public static readonly Expression<Func<KnowledgeDocument, KnowledgeDocumentLlmMarkdownDto>> ToLlmMarkdownDtoExpression = x => new KnowledgeDocumentLlmMarkdownDto
    {
        Id = x.Id,
        MarkdownText = x.MarkdownText ?? string.Empty,
        Draft = x.MarkdownLlmDraft,
        PartsDone = x.MarkdownLlmPartsDone,
        PartsTotal = x.MarkdownLlmPartsTotal
    };

    private static readonly Func<KnowledgeDocument, KnowledgeDocumentDto> CompiledToDto = ToDtoExpression.Compile();

    public static KnowledgeDocumentDto ToDto(this KnowledgeDocument entity) => CompiledToDto(entity);

    public static KnowledgeDocument ToEntity(this CreateKnowledgeDocumentDto dto, string relativePath) => new()
    {
        ApiKeyId = dto.ApiKeyId,
        KnowledgeGroupId = dto.KnowledgeGroupId,
        OriginalFileName = dto.OriginalFileName,
        RelativePath = relativePath,
        ContentType = dto.ContentType,
        SizeBytes = dto.SizeBytes,
        Status = KnowledgeDocumentStatus.Processing
    };

    public static KnowledgeDocumentFileDto ToFileDto(string absolutePath, string originalFileName, string contentType) => new()
    {
        AbsolutePath = absolutePath,
        OriginalFileName = originalFileName,
        ContentType = contentType
    };
}
