using System.Text.Json.Serialization;

namespace api.Managers.KnowledgeBase.Dtos;

// The model's JSON answer in AgentChat's first step. Two concrete properties instead of one "ready?" verdict,
// which a local model judges far less reliably (it tends to ask questions back about clear questions).
public class AgentAssessmentDto
{
    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    // It is clear what the user wants to find out.
    [JsonPropertyName("has_clear_goal")]
    public bool HasClearGoal { get; set; }

    // The request covers so much that one answer can't do it justice.
    [JsonPropertyName("too_broad")]
    public bool TooBroad { get; set; }

    [JsonPropertyName("questions")]
    public List<string> Questions { get; set; } = [];

    [JsonIgnore]
    public bool IsReady => HasClearGoal && !TooBroad;
}
