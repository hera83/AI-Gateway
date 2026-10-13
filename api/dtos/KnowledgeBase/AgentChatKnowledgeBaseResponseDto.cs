namespace api.Dtos.KnowledgeBase;

public class AgentChatKnowledgeBaseResponseDto
{
    // "NeedsClarification": Message holds questions; answer them and send the conversation again.
    // "Answered": Message holds the researched answer in Markdown, citing Sources as [n].
    public string Status { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    // The research task as the agent understood it from the conversation.
    public string Task { get; set; } = string.Empty;

    // The assistant turn to add to the conversation.
    public ChatMessageDto Message { get; set; } = new();

    // Only when Status is "NeedsClarification".
    public List<string> Questions { get; set; } = [];

    // The searches the agent ran, in order.
    public List<string> Queries { get; set; } = [];

    // [n] in the answer is the source with Number n.
    public List<AgentSourceDto> Sources { get; set; } = [];
}
