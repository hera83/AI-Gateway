namespace api.Managers.KnowledgeBase.Dtos;

public enum UploadDocumentOutcome
{
    // Stored. The document's own Status says whether indexing succeeded (Indexed) or not (Failed).
    Uploaded,
    GroupNotFound,
    TooLarge,
    UnsupportedFileType
}
