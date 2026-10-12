using System.Drawing;
using System.Security.Cryptography;
using api.Managers.KnowledgeBase.Dtos;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace api.Managers.KnowledgeBase;

// Only used by the knowledge base, so it lives with the feature instead of in extensions/.
public static class DocumentTextExtractor
{
    private static readonly string[] SupportedExtensions = [".txt", ".md", ".pdf", ".docx"];

    public static bool IsSupported(string fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public static bool IsPdf(string fileName) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    // PDFs are read with ReadPdfPages instead, so KnowledgeBaseManager can OCR their scanned pages and images.
    public static async Task<string> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".txt" or ".md" => await new StreamReader(fileStream).ReadToEndAsync(cancellationToken),
            ".docx" => ExtractDocxText(fileStream),
            _ => throw new NotSupportedException($"File '{fileName}' has an unsupported file type.")
        };

    // The text layer of every page (in reading order, with spaces worked out from the glyph positions) and
    // where each image is drawn. The stream is left open, so it can be rendered afterwards.
    public static List<PdfPageDto> ReadPdfPages(Stream fileStream)
    {
        using var document = PdfDocument.Open(fileStream);
        return document.GetPages().Select(ToPdfPageDto).ToList();
    }

    private static PdfPageDto ToPdfPageDto(Page page)
    {
        // PdfPig hands out content positions (images, letters) already turned by /Rotate and shifted so the
        // media box starts at 0,0 (origin bottom-left, y up). The crop box, which is what renderers draw, is
        // still in raw PDF space, so it is moved into the same space here.
        var quarterTurns = (page.Rotation.Value / 90 % 4 + 4) % 4;
        var mediaBox = page.MediaBox.Bounds;
        var media = ToPageSpace(mediaBox, mediaBox, quarterTurns);
        var crop = ToPageSpace(page.CropBox.Bounds, mediaBox, quarterTurns);
        crop = (Math.Max(crop.Left, media.Left), Math.Max(crop.Bottom, media.Bottom), Math.Min(crop.Right, media.Right), Math.Min(crop.Top, media.Top));

        var pageDto = new PdfPageDto
        {
            Number = page.Number,
            Width = (float)(crop.Right - crop.Left),
            Height = (float)(crop.Top - crop.Bottom),
            Text = ContentOrderTextExtractor.GetText(page)
        };

        var pageBounds = new RectangleF(0, 0, pageDto.Width, pageDto.Height);
        foreach (var image in page.GetImages())
        {
            // To the displayed page: origin top-left, y down, relative to the crop box.
            var extent = Extent(Corners(image.BoundingBox));
            var bounds = RectangleF.Intersect(
                RectangleF.FromLTRB(
                    (float)(extent.Left - crop.Left),
                    (float)(crop.Top - extent.Top),
                    (float)(extent.Right - crop.Left),
                    (float)(crop.Top - extent.Bottom)),
                pageBounds);

            if (bounds.Width > 0 && bounds.Height > 0)
            {
                pageDto.Images.Add(new PdfImageDto
                {
                    Bounds = bounds,
                    ContentHash = Convert.ToHexString(SHA256.HashData(image.RawMemory.Span))
                });
            }
        }

        return pageDto;
    }

    // Raw PDF space → PdfPig's page space: shifted to the media box origin, then turned clockwise by /Rotate.
    private static (double Left, double Bottom, double Right, double Top) ToPageSpace(PdfRectangle box, PdfRectangle mediaBox, int quarterTurns)
    {
        double width = mediaBox.Width, height = mediaBox.Height;
        return Extent(Corners(box).Select(point =>
        {
            double x = point.X - mediaBox.Left, y = point.Y - mediaBox.Bottom;
            return quarterTurns switch
            {
                1 => (y, width - x),
                2 => (width - x, height - y),
                3 => (height - y, x),
                _ => (x, y)
            };
        }));
    }

    private static IEnumerable<(double X, double Y)> Corners(PdfRectangle rectangle) =>
    [
        (rectangle.TopLeft.X, rectangle.TopLeft.Y),
        (rectangle.TopRight.X, rectangle.TopRight.Y),
        (rectangle.BottomLeft.X, rectangle.BottomLeft.Y),
        (rectangle.BottomRight.X, rectangle.BottomRight.Y)
    ];

    // PdfRectangle's Left/Right/Top/Bottom follow its corners, so they are swapped on rotated pages.
    private static (double Left, double Bottom, double Right, double Top) Extent(IEnumerable<(double X, double Y)> points)
    {
        var list = points.ToList();
        return (list.Min(point => point.X), list.Min(point => point.Y), list.Max(point => point.X), list.Max(point => point.Y));
    }

    private static string ExtractDocxText(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body is null
            ? string.Empty
            : string.Join(Environment.NewLine, body.Elements<Paragraph>().Select(paragraph => paragraph.InnerText));
    }
}
