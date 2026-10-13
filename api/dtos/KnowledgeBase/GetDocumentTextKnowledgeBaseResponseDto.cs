namespace api.Dtos.KnowledgeBase;

public class GetDocumentTextKnowledgeBaseResponseDto
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public string FileName { get; set; } = string.Empty;

    // Processing, Indexed or Failed. Text is only set when Indexed.
    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    // All text extracted from the file (incl. OCR), as it was chunked and indexed.
    public string? Text { get; set; }
}
