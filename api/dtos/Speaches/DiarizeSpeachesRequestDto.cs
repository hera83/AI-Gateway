namespace api.Dtos.Speaches;

public class DiarizeSpeachesRequestDto
{
    public IFormFile File { get; set; } = null!;

    public List<string>? KnownSpeakerNames { get; set; }

    public List<string>? KnownSpeakerReferences { get; set; }
}
