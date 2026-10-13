namespace api.Managers.KnowledgeBase.Dtos;

// One block of text on a PDF page (a heading, a paragraph, a list), as PdfPig's layout analysis finds it.
// PdfMarkdownConverter turns the blocks into Markdown once every page is read.
public class PdfTextBlockDto
{
    public List<string> Lines { get; set; } = [];

    // Median point size of the block's letters.
    public double FontSize { get; set; }

    // At least 80 % of the letters are in a bold font.
    public bool IsBold { get; set; }

    // Within the top or bottom 8 % of the page, where page numbers, headers and footers sit.
    public bool IsInMargin { get; set; }
}
