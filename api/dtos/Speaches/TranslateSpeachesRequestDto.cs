namespace api.Dtos.Speaches;

public class TranslateSpeachesRequestDto
{
    public string Model { get; set; } = string.Empty;

    public IFormFile File { get; set; } = null!;

    public string? Prompt { get; set; }

    public double? Temperature { get; set; }
}
