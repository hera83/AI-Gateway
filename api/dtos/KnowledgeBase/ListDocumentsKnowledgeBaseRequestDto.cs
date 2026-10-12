namespace api.Dtos.KnowledgeBase;

public class ListDocumentsKnowledgeBaseRequestDto
{
    // Optional: only documents in this group.
    public Guid? GroupId { get; set; }
}
