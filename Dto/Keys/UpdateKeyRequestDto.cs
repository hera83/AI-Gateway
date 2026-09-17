using System.ComponentModel.DataAnnotations;

namespace AiGateway.Dto.Keys;

public class UpdateKeyRequestDto : IValidatableObject
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

    // See CreateKeyRequestDto.Validate — same rationale, so an update can't silently expire a key immediately either.
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
