namespace api.Managers.KnowledgeBase.Dtos;

public enum AgentChatOutcome
{
    // The task isn't clear enough to research; the agent asks questions back.
    NeedsClarification,

    // The task was researched; the answer cites the sources.
    Answered
}
