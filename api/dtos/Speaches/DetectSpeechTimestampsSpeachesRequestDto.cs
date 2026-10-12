namespace api.Dtos.Speaches;

public class DetectSpeechTimestampsSpeachesRequestDto
{
    public IFormFile File { get; set; } = null!;

    public string? Model { get; set; }

    public double? Threshold { get; set; }

    public double? NegThreshold { get; set; }

    public int? MinSpeechDurationMs { get; set; }

    public double? MaxSpeechDurationS { get; set; }

    public int? MinSilenceDurationMs { get; set; }

    public int? SpeechPadMs { get; set; }
}
