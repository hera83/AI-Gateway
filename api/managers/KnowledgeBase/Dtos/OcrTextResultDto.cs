namespace api.Managers.KnowledgeBase.Dtos;

// What OcrTextBuilder made of one Tesseract result.
public class OcrTextResultDto
{
    // Only the words and lines that passed the confidence filters.
    public string Text { get; set; } = string.Empty;

    // Words Tesseract found before any filtering. 0 = blank page, or nothing it could read at all.
    public int RecognizedWordCount { get; set; }

    // Length-weighted mean word confidence (0-100) of Text.
    public double Confidence { get; set; }

    // null = good enough to index.
    public string? RejectionReason { get; set; }

    public bool IsAccepted => RejectionReason is null;
}
