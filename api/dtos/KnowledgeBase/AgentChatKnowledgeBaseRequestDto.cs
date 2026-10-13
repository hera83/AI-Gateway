using System.ComponentModel.DataAnnotations;

namespace api.Dtos.KnowledgeBase;

public class AgentChatKnowledgeBaseRequestDto : IValidatableObject
{
    // The Ollama chat model the agent thinks and writes with, e.g. "qwen2.5:14b-instruct".
    [Required]
    public string Model { get; set; } = string.Empty;

    // Full conversation, oldest first, ending with the latest user turn. When the agent asks questions back
    // (status "NeedsClarification"), add its message and the user's answer and send the whole conversation again.
    [Required]
    [MinLength(1)]
    public List<ChatMessageDto> Messages { get; set; } = [];

    // Optional: only research documents in this group. Without it, every group the key owns is researched.
    public Guid? GroupId { get; set; }

    // Research at once, without first checking whether the task is clear enough (no questions back).
    public bool SkipClarification { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Messages.Any(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult(
                "Messages must include at least one message with role 'user' with the task to research.",
                [nameof(Messages)]);
        }
    }
}
