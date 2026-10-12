namespace api.Dtos.Speaches;

public class TranscriptionWordDto
{
    public string Word { get; set; } = string.Empty;

    public double Start { get; set; }

    public double End { get; set; }
}
