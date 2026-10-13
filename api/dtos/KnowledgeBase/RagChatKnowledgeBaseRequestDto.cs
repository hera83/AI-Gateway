using System.ComponentModel.DataAnnotations;

namespace api.Dtos.KnowledgeBase;

public class RagChatKnowledgeBaseRequestDto : IValidatableObject
{
    [Required]
    public string Model { get; set; } = string.Empty;

    // Full conversation, oldest first, ending with the latest user turn (same contract as Ollama/Chat).
    // Retrieval uses only the latest "user" message, and a fresh system message with the retrieved
    // context replaces any system message sent here.
    [Required]
    [MinLength(1)]
    public List<ChatMessageDto> Messages { get; set; } = [];

    [Range(1, 100)]
    public int? TopK { get; set; }

    // Optional: only retrieve context from documents in this group. Without it, every group the key owns is searched.
    public Guid? GroupId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Messages.Any(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult(
                "Messages must include at least one message with role 'user' to use as the retrieval query.",
                [nameof(Messages)]);
        }
    }
}
