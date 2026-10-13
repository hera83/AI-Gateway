namespace api.Managers.KnowledgeBase.Dtos;

public class RagChatDto
{
    public Guid ApiKeyId { get; set; }

    public string Model { get; set; } = string.Empty;

    // Full conversation, oldest first. Must contain at least one "user" message.
    public List<RagChatMessageDto> Messages { get; set; } = [];

    public int? TopK { get; set; }

    // Optional: only retrieve context from this group.
    public Guid? KnowledgeGroupId { get; set; }
}
