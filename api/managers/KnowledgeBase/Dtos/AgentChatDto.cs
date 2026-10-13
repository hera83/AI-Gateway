namespace api.Managers.KnowledgeBase.Dtos;

public class AgentChatDto
{
    public Guid ApiKeyId { get; set; }

    public string Model { get; set; } = string.Empty;

    // Full conversation, oldest first, ending with the latest user turn. Clarifying questions and their answers are
    // part of it, so the agent sees what the user has already explained.
    public List<RagChatMessageDto> Messages { get; set; } = [];

    // Optional: only research documents in this group.
    public Guid? KnowledgeGroupId { get; set; }

    // Research at once, without checking whether the task is clear enough.
    public bool SkipClarification { get; set; }
}
