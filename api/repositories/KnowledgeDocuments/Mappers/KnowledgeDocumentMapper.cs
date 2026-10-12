using System.Linq.Expressions;
using api.Data.Models;
using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Repositories.KnowledgeDocuments.Mappers;

public static class KnowledgeDocumentMapper
{
    public static readonly Expression<Func<KnowledgeDocument, KnowledgeDocumentDto>> ToDtoExpression = x => new KnowledgeDocumentDto
    {
        Id = x.Id,
        KnowledgeGroupId = x.KnowledgeGroupId,
        OriginalFileName = x.OriginalFileName,
        ContentType = x.ContentType,
        SizeBytes = x.SizeBytes,
        Status = x.Status.ToString(),
        ErrorMessage = x.ErrorMessage,
        CreatedAt = x.CreatedAt
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
