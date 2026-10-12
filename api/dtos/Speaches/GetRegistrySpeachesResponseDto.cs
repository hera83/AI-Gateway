namespace api.Dtos.Speaches;

public class GetRegistrySpeachesResponseDto
{
    public List<ModelDto> Data { get; set; } = [];

    public string Object { get; set; } = "list";
}
