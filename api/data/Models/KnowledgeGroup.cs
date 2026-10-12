namespace api.Data.Models;

public class KnowledgeGroup : EntityBase
{
    public Guid ApiKeyId { get; set; }

    public string Name { get; set; } = string.Empty;
}
