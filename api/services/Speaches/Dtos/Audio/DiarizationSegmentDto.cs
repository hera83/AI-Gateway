namespace api.Services.Speaches.Dtos.Audio;

public class DiarizationSegmentDto
{
    public double Start { get; set; }

    public double End { get; set; }

    public string Speaker { get; set; } = string.Empty;
}
