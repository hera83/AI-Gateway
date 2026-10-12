namespace api.Dtos.Speaches;

public class ListModelsSpeachesResponseDto
{
    public List<ModelDto> Data { get; set; } = [];

    public string Object { get; set; } = "list";
}
