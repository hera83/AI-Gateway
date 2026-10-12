namespace api.Services.Speaches.Dtos.Audio;

public class TranscriptionSegmentDto
{
    public int Id { get; set; }

    public double Start { get; set; }

    public double End { get; set; }

    public string Text { get; set; } = string.Empty;

    public List<int> Tokens { get; set; } = [];

    public double Temperature { get; set; }

    public double AvgLogprob { get; set; }

    public double CompressionRatio { get; set; }

    public double NoSpeechProb { get; set; }

    public int Seek { get; set; }
}
