using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Managers.KnowledgeBase.Dtos;

public class UploadDocumentResultDto
{
    public UploadDocumentOutcome Outcome { get; set; }

    // Only set when Outcome is Uploaded.
    public KnowledgeDocumentDto? Document { get; set; }
}
