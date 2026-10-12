namespace api.Dtos.Speaches;

public class ListVoicesSpeachesResponseDto
{
    public List<VoiceDto> Voices { get; set; } = [];

    public string Object { get; set; } = "list";
}
