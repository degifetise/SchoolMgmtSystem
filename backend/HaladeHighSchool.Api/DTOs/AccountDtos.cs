using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

/// <summary>
/// The only two values the change-password endpoint accepts.
///
/// There is deliberately no email, name or role here. The endpoint reads the account from the
/// bearer token and touches nothing but the password hash, so this DTO cannot be used to edit
/// an identity attribute even if extra JSON is posted - unbound properties are ignored.
/// </summary>
public record ChangePasswordRequest : IValidatableObject
{
    [Required(ErrorMessage = "Your current password is required.")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Choose a new password.")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters.")]
    [MaxLength(100)]
    public string NewPassword { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Caught here rather than by Identity, which would report it as a policy failure.
        if (string.Equals(CurrentPassword, NewPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "The new password has to be different from the current one.",
                [nameof(NewPassword)]);
        }
    }
}

/// <summary>
/// Confirmation that the password changed, with the audit row's timestamp so a client can show
/// exactly what was recorded.
/// </summary>
public record ChangePasswordResponse
{
    public DateTime ChangedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
