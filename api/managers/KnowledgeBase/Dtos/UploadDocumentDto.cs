namespace api.Managers.KnowledgeBase.Dtos;

public class UploadDocumentDto
{
    public Guid ApiKeyId { get; set; }

    public Guid KnowledgeGroupId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public Stream Content { get; set; } = Stream.Null;
}
