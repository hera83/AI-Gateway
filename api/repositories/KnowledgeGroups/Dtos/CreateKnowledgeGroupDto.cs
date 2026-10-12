namespace api.Repositories.KnowledgeGroups.Dtos;

public class CreateKnowledgeGroupDto
{
    public Guid ApiKeyId { get; set; }

    public string Name { get; set; } = string.Empty;
}
