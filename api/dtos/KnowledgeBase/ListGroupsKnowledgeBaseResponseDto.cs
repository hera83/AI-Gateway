namespace api.Dtos.KnowledgeBase;

public class ListGroupsKnowledgeBaseResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
