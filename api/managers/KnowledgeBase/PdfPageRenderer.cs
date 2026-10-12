using System.Drawing;
using System.Runtime.Versioning;
using api.Managers.KnowledgeBase.Dtos;
using PDFtoImage;
using SkiaSharp;

namespace api.Managers.KnowledgeBase;

// Renders PDF pages with PDFium, so OCR sees exactly what a reader sees, whatever the image format
// (JPEG, JBIG2, CCITT …), masks or transformations.
public static class PdfPageRenderer
{
    // Keeps posters and drawings from becoming gigapixel bitmaps. Tesseract's own limit is 32767.
    private const int MaxPixelSide = 8000;

    // White border around image crops. Tesseract misreads text that touches the edge of the image.
    private const int CropPadding = 16;

    // PDFium is not thread-safe, and several uploads can render at the same time.
    private static readonly Lock RenderLock = new();

    public static int GetEffectiveDpi(PdfPageDto page, int dpi) =>
        (int)Math.Max(1, Math.Min(dpi, MaxPixelSide * 72.0 / Math.Max(page.Width, page.Height)));

    public static SKBitmap Render(Stream pdfStream, PdfPageDto page, int dpi)
    {
        // PDFium ships native binaries for these platforms only.
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return RenderPage(pdfStream, page, dpi);
        }

        throw new PlatformNotSupportedException("Rendering PDF pages requires Windows, Linux or macOS.");
    }

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static SKBitmap RenderPage(Stream pdfStream, PdfPageDto page, int dpi)
    {
        lock (RenderLock)
        {
            pdfStream.Position = 0;
            return Conversion.ToImage(
                pdfStream,
                page: page.Number - 1,
                leaveOpen: true,
                options: new RenderOptions(Dpi: dpi, WithAnnotations: true, Grayscale: true));
        }
    }

    // region is in points from the page's top-left corner (PdfImageDto.Bounds). null = the whole page.
    public static byte[] EncodePng(SKBitmap pageBitmap, PdfPageDto page, RectangleF? region = null)
    {
        if (region is not { } bounds)
        {
            using var pageData = pageBitmap.Encode(SKEncodedImageFormat.Png, 100);
            return pageData.ToArray();
        }

        var scaleX = pageBitmap.Width / page.Width;
        var scaleY = pageBitmap.Height / page.Height;
        var pixels = SKRectI.Intersect(
            new SKRectI(
                (int)Math.Floor(bounds.Left * scaleX),
                (int)Math.Floor(bounds.Top * scaleY),
                (int)Math.Ceiling(bounds.Right * scaleX),
                (int)Math.Ceiling(bounds.Bottom * scaleY)),
            new SKRectI(0, 0, pageBitmap.Width, pageBitmap.Height));

        if (pixels.IsEmpty)
        {
            return [];
        }

        using var crop = new SKBitmap(pixels.Width + 2 * CropPadding, pixels.Height + 2 * CropPadding, pageBitmap.ColorType, pageBitmap.AlphaType);
        using (var canvas = new SKCanvas(crop))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(pageBitmap, pixels, SKRect.Create(CropPadding, CropPadding, pixels.Width, pixels.Height), SKSamplingOptions.Default);
        }

        using var cropData = crop.Encode(SKEncodedImageFormat.Png, 100);
        return cropData.ToArray();
    }
}
