namespace api.Repositories.KnowledgeDocuments.Dtos;

public class KnowledgeDocumentFileDto
{
    public string AbsolutePath { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
}
