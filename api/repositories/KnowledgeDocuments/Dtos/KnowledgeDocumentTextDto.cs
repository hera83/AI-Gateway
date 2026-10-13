namespace api.Repositories.KnowledgeDocuments.Dtos;

public class KnowledgeDocumentTextDto
{
    public Guid Id { get; set; }

    public Guid KnowledgeGroupId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    // null until the document is Indexed.
    public string? ExtractedText { get; set; }
}
