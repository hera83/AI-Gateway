namespace api.Managers.KnowledgeBase.Dtos;

// One PDF page as DocumentTextExtractor reads it. Sizes are in points (1/72 inch), in the page's displayed
// orientation, which is also how PdfPageRenderer renders it.
public class PdfPageDto
{
    public int Number { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    // The PDF's own text layer. Empty for scanned pages.
    public string Text { get; set; } = string.Empty;

    public List<PdfImageDto> Images { get; set; } = [];

    // The text layer split into blocks in reading order, for Markdown. null if the layout analysis failed.
    public List<PdfTextBlockDto>? Blocks { get; set; }
}
