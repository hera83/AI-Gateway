namespace api.Services.Speaches.Dtos.Audio;

public class DiarizationResponseDto
{
    public double Duration { get; set; }

    public List<DiarizationSegmentDto> Segments { get; set; } = [];
}
