using System.ComponentModel.DataAnnotations;

namespace AiGateway.Dto.Keys;

public class CreateKeyRequestDto : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ResponsibleName { get; set; }

    [Required]
    [MaxLength(300)]
    public required string ContactInfo { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    // Without this, a caller (or Swagger UI's auto-generated "now" example value for date-time
    // fields) can create a key with an ExpiresAt already at/before the current time — it's accepted
    // with 201 Created but is immediately rejected on first use, indistinguishable from a missing
    // header from ApiKeyAuthenticationHandler's generic error message.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
        {
            yield return new ValidationResult(
                "ExpiresAt must be in the future.",
                [nameof(ExpiresAt)]);
        }
    }
}
