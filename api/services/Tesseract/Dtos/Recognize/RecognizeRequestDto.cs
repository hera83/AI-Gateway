namespace api.Services.Tesseract.Dtos.Recognize;

public class RecognizeRequestDto
{
    // Any format Leptonica reads (PNG, JPEG, TIFF, BMP, GIF, WebP, JPEG 2000, PNM). A multi-page TIFF gives
    // words from every page (WordDto.PageNumber).
    public byte[] Image { get; set; } = [];

    // Language codes joined with '+', e.g. "dan+eng". Each needs a <code>.traineddata in tessdata.
    public string Languages { get; set; } = "eng";

    // The text size estimate depends on it. Set it for rendered images (PDF pages), whose own metadata is unreliable.
    // null = the image's own resolution, or Tesseract estimates it from the text size when the image has none.
    public int? Dpi { get; set; }

    // --psm. 3 = fully automatic page segmentation (Tesseract's default).
    public int PageSegmentationMode { get; set; } = 3;
}
