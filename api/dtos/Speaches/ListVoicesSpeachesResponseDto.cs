namespace api.Dtos.Speaches;

public class ListVoicesSpeachesResponseDto
{
    public List<ModelDto> Data { get; set; } = [];

    public string Object { get; set; } = "list";
}
