using api.Services.Tesseract.Dtos.Recognize;

namespace api.Services.Tesseract.Interfaces;

public interface ITesseractService
{
    Task<RecognizeResponseDto> RecognizeAsync(RecognizeRequestDto request, CancellationToken cancellationToken = default);
}
