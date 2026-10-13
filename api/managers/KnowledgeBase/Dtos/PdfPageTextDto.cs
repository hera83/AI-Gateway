namespace api.Managers.KnowledgeBase.Dtos;

// The final text of one PDF page plus what OCR did on it, summed up per document for logging.
public class PdfPageTextDto
{
    public string Text { get; set; } = string.Empty;

    // The page's own text layer is part of Text (first, before any OCR text).
    public bool UsesTextLayer { get; set; }

    // The OCR results used in Text, in order.
    public List<string> OcrTexts { get; set; } = [];

    public bool IsWholePageOcr { get; set; }

    public int OcrAccepted { get; set; }

    public int OcrRejected { get; set; }

    // OCR text that was already in the page's own text, so it was left out.
    public int OcrDuplicates { get; set; }

    public List<string> OcrErrors { get; set; } = [];
}
