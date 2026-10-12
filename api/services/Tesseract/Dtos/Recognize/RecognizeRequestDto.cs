namespace api.Services.Tesseract.Dtos.Recognize;

public class RecognizeRequestDto
{
    // Any format Leptonica reads (PNG, JPEG, TIFF, BMP …).
    public byte[] Image { get; set; } = [];

    // Language codes joined with '+', e.g. "dan+eng". Each needs a <code>.traineddata in tessdata.
    public string Languages { get; set; } = "eng";

    // Tesseract can't read the resolution from piped images, and the text size estimate depends on it.
    public int Dpi { get; set; } = 300;

    // --psm. 3 = fully automatic page segmentation (Tesseract's default).
    public int PageSegmentationMode { get; set; } = 3;
}
