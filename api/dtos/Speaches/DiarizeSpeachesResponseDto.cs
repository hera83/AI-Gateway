namespace api.Dtos.Speaches;

public class DiarizeSpeachesResponseDto
{
    public double Duration { get; set; }

    public List<DiarizationSegmentDto> Segments { get; set; } = [];
}
