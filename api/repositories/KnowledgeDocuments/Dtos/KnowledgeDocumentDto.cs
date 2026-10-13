namespace api.Repositories.KnowledgeDocuments.Dtos;

public class KnowledgeDocumentDto
{
    public Guid Id { get; set; }

    public Guid ApiKeyId { get; set; }

    public Guid KnowledgeGroupId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
}
