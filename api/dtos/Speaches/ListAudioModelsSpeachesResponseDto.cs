namespace api.Dtos.Speaches;

public class ListAudioModelsSpeachesResponseDto
{
    public List<ModelDto> Models { get; set; } = [];

    public string Object { get; set; } = "list";
}
