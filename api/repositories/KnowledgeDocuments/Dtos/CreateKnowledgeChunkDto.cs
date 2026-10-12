namespace api.Repositories.KnowledgeDocuments.Dtos;

public class CreateKnowledgeChunkDto
{
    public string Text { get; set; } = string.Empty;

    public List<double> Embedding { get; set; } = [];
}
