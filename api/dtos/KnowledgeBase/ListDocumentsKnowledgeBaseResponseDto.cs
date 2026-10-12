namespace api.Dtos.KnowledgeBase;

public class ListDocumentsKnowledgeBaseResponseDto
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public DateTime UploadedAt { get; set; }
}
