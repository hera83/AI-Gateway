namespace api.Data.Models;

public class KnowledgeDocument : EntityBase
{
    public Guid ApiKeyId { get; set; }

    public Guid KnowledgeGroupId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? ExtractedText { get; set; }

    public KnowledgeDocumentStatus Status { get; set; }

    public string? ErrorMessage { get; set; }
}
