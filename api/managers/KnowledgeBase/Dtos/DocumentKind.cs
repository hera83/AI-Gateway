namespace api.Managers.KnowledgeBase.Dtos;

// How DocumentTextExtractor reads an upload. Decided from the extension, and for everything else from the content.
public enum DocumentKind
{
    Unsupported,
    Text,
    Pdf,
    Docx,
    Image
}
