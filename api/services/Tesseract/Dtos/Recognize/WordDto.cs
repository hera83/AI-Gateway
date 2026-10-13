namespace api.Services.Tesseract.Dtos.Recognize;

// One word row (level 5) from Tesseract's TSV output. Block, paragraph and line numbers group words into layout.
public class WordDto
{
    // 1-based. Only above 1 for multi-page TIFFs. Block, paragraph and line numbers start over on each page.
    public int PageNumber { get; set; }

    public int BlockNumber { get; set; }

    public int ParagraphNumber { get; set; }

    public int LineNumber { get; set; }

    public int WordNumber { get; set; }

    // Pixel position in the image.
    public int Left { get; set; }

    public int Top { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    // 0-100. How sure Tesseract is of this word.
    public double Confidence { get; set; }

    public string Text { get; set; } = string.Empty;
}
