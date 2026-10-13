namespace api.Dtos.KnowledgeBase;

public class AgentSourceDto
{
    // The [n] the answer cites it by.
    public int Number { get; set; }

    // Whether the answer actually cites it.
    public bool Cited { get; set; }

    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public string DocumentFileName { get; set; } = string.Empty;

    public Guid GroupId { get; set; }

    // The excerpt, starting with the headings of the section it comes from.
    public string Text { get; set; } = string.Empty;

    // Higher is more relevant (1 - cosine distance).
    public double Score { get; set; }
}
