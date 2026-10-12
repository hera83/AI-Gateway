namespace api.Repositories.KnowledgeDocuments.Dtos;

public class CreateKnowledgeDocumentDto
{
    public Guid ApiKeyId { get; set; }

    public Guid KnowledgeGroupId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public Stream Content { get; set; } = Stream.Null;
}
