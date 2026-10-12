namespace api.Managers.KnowledgeBase.Dtos;

// The final text of one PDF page plus what OCR did on it, summed up per document for logging.
public class PdfPageTextDto
{
    public string Text { get; set; } = string.Empty;

    public bool IsWholePageOcr { get; set; }

    public int OcrAccepted { get; set; }

    public int OcrRejected { get; set; }

    // OCR text that was already in the page's own text, so it was left out.
    public int OcrDuplicates { get; set; }

    public List<string> OcrErrors { get; set; } = [];
}
