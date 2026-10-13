using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Managers.KnowledgeBase.Dtos;

public class AgentChatResultDto
{
    public AgentChatOutcome Outcome { get; set; }

    public string Model { get; set; } = string.Empty;

    // The research task as the agent understood it.
    public string Task { get; set; } = string.Empty;

    // The assistant's message: the questions when NeedsClarification, the Markdown answer when Answered.
    public string Message { get; set; } = string.Empty;

    // Only when NeedsClarification.
    public List<string> Questions { get; set; } = [];

    // The searches the agent ran, in order.
    public List<string> Queries { get; set; } = [];

    // The excerpts the answer was written from; [n] in the answer is Sources[n - 1].
    public List<KnowledgeChunkMatchDto> Sources { get; set; } = [];
}
