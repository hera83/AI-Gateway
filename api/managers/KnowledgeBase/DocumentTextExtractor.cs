using System.Drawing;
using System.Security.Cryptography;
using System.Text;
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
    // Everything Leptonica (Tesseract's image library) reads. Tested with the Windows build in services/Tesseract/Engine;
    // Ubuntu's Leptonica package is built with the same image libraries (png, jpeg, tiff, gif, webp, openjp2).
    public static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".jfif", ".tif", ".tiff", ".bmp", ".gif", ".webp", ".jp2", ".pnm", ".pbm", ".pgm", ".ppm"];

    // How much of a file without a known extension is looked at to decide whether it is text.
    private const int TextSniffBytes = 8192;

    // Control characters other than tab, line breaks, form feed and escape. A few are tolerated, many mean binary.
    private const double MaxControlCharacterShare = 0.05;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Text files that aren't valid UTF-8 are almost always older Windows files (æøå, “”, €).
    private static readonly Encoding LegacyEncoding = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    // PDF, Word and images by extension. Anything else is accepted as text when the content looks like text, so
    // every plain-text format works (.csv, .json, .xml, .html, .log, source code, files without an extension …).
    // The stream must be seekable; it is put back at the start.
    public static async Task<DocumentKind> DetectKindAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".pdf")
        {
            return DocumentKind.Pdf;
        }

        if (extension == ".docx")
        {
            return DocumentKind.Docx;
        }

        if (ImageExtensions.Contains(extension))
        {
            return DocumentKind.Image;
        }

        var header = new byte[TextSniffBytes];
        var length = await fileStream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        fileStream.Position = 0;
        return LooksLikeText(header.AsSpan(0, length)) ? DocumentKind.Text : DocumentKind.Unsupported;
    }

    // PDFs and images are read by KnowledgeBaseManager instead, so it can OCR them.
    public static async Task<string> ExtractTextAsync(Stream fileStream, DocumentKind kind, CancellationToken cancellationToken = default) =>
        kind switch
        {
            DocumentKind.Text => await ReadTextAsync(fileStream, cancellationToken),
            DocumentKind.Docx => ExtractDocxText(fileStream),
            _ => throw new NotSupportedException($"Documents of kind '{kind}' are not read as plain text.")
        };

    // A byte-order mark decides UTF-8/16/32. Without one, UTF-8 is tried first and Windows-1252 is the fallback.
    private static async Task<string> ReadTextAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory, cancellationToken);
        try
        {
            memory.Position = 0;
            using var reader = new StreamReader(memory, StrictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return LegacyEncoding.GetString(memory.GetBuffer(), 0, (int)memory.Length);
        }
    }

    // Text has no NUL bytes (except UTF-16/32, which starts with a byte-order mark) and few control characters.
    private static bool LooksLikeText(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) || header.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]) ||
            header.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0xFE, 0xFF]))
        {
            return true;
        }

        if (header.Contains((byte)0))
        {
            return false;
        }

        var controlCharacters = 0;
        foreach (var value in header)
        {
            if (value < 0x20 && value is not ((byte)'\t' or (byte)'\n' or (byte)'\r' or 0x0C or 0x1B))
            {
                controlCharacters++;
            }
        }

        return controlCharacters <= header.Length * MaxControlCharacterShare;
    }

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

        // Layout analysis is only for the Markdown version, so it may never cost the text: if it fails, the
        // Markdown falls back to the page's plain text.
        try
        {
            pageDto.Blocks = PdfMarkdownConverter.ReadBlocks(page, crop);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            pageDto.Blocks = null;
        }

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

    // Descendants, so text in tables and content controls is included too.
    private static string ExtractDocxText(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body is null
            ? string.Empty
            : string.Join(Environment.NewLine, body.Descendants<Paragraph>().Select(paragraph => paragraph.InnerText));
    }
}
