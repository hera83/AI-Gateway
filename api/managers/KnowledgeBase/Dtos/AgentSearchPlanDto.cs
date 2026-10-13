using System.Text.Json.Serialization;

namespace api.Managers.KnowledgeBase.Dtos;

// The model's JSON answer when it plans (or, after reading, adds) search queries. Enough is only used after reading.
public class AgentSearchPlanDto
{
    [JsonPropertyName("enough")]
    public bool Enough { get; set; }

    [JsonPropertyName("queries")]
    public List<string> Queries { get; set; } = [];
}
