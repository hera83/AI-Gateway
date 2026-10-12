namespace api.Managers.KnowledgeBase;

// Bound from "KnowledgeBase:Ocr". How PDFs are OCR'ed with Tesseract and how strict the quality check is.
public class KnowledgeBaseOcrSettings
{
    // false = PDFs only use their own text layer, as before OCR existed.
    public bool Enabled { get; set; } = true;

    // Tesseract language codes joined with '+'. Each needs a <code>.traineddata where Tesseract looks for them.
    // Use the tessdata_best models: the standard ones often lose æ, ø and å as soon as English is also loaded.
    public string Languages { get; set; } = "dan+eng";

    // Pages are rendered at this resolution before OCR. 300 is Tesseract's sweet spot.
    public int RenderDpi { get; set; } = 300;

    // Mean word confidence (0-100) a page or image must reach before its OCR text is used.
    public int MinConfidence { get; set; } = 70;

    // Single words below this confidence are dropped first. Typically specks and edges read as "|", ";" or "6,".
    public int MinWordConfidence { get; set; } = 50;

    // Lines whose remaining words average below this (typically noise from photos and graphics) are dropped next.
    public int MinLineConfidence { get; set; } = 60;

    // Share (%) of the recognized text that must survive the word and line filters. Good pages keep nearly
    // everything, upside-down or garbled ones lose most of it.
    public int MinKeptPercent { get; set; } = 60;

    // Fewer letters and digits than this counts as "no text", both for a page's text layer and for OCR results.
    public int MinCharacters { get; set; } = 20;

    // Pages OCR'ed at the same time. 0 = half of the CPU cores.
    public int MaxParallelism { get; set; }
}
