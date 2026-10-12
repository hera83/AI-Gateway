using System.Drawing;

namespace api.Managers.KnowledgeBase.Dtos;

public class PdfImageDto
{
    // Where the image is drawn, in points from the page's top-left corner (clipped to the page).
    public RectangleF Bounds { get; set; }

    // SHA-256 of the image data, so an image repeated on many pages (a logo, a header) is only OCR'ed once.
    public string ContentHash { get; set; } = string.Empty;
}
