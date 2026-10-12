namespace api.Dtos.Speaches;

public class GetModelSpeachesResponseDto
{
    public string Id { get; set; } = string.Empty;

    public long Created { get; set; }

    public string Object { get; set; } = "model";

    public string OwnedBy { get; set; } = string.Empty;

    public List<string>? Language { get; set; }

    public string Task { get; set; } = string.Empty;
}
