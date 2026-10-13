namespace api.Managers.KnowledgeBase;

// Bound from "KnowledgeBase:Agent". Limits for AgentChat's research loop; every round costs LLM calls.
public class KnowledgeBaseAgentSettings
{
    // Search, read, decide whether it's enough. A second round searches for what the first one missed.
    public int MaxSearchRounds { get; set; } = 2;

    // Search queries the model writes per round, with different wording, so the weak spots of the embedding model
    // (other words than the documents use) matter less.
    public int QueriesPerRound { get; set; } = 4;

    public int ResultsPerQuery { get; set; } = 5;

    // The most relevant excerpts the answer is written from.
    public int MaxSources { get; set; } = 12;
}
