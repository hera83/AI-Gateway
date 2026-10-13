namespace api.Managers.KnowledgeBase.Dtos;

// One upload's content, made in the same pass: plain text for chunking/embedding and Markdown for reading.
public class ExtractedDocumentDto
{
    public string Text { get; set; } = string.Empty;

    public string Markdown { get; set; } = string.Empty;

    // Share of Text (0-1) that came from OCR. Decides whether an LLM lays out the Markdown afterwards.
    public double OcrShare { get; set; }
}
