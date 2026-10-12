namespace api.Services.Tesseract.Dtos.Recognize;

public class RecognizeResponseDto
{
    // In reading order, as Tesseract's TSV output lists them.
    public List<WordDto> Words { get; set; } = [];
}
