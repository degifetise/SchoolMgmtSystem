using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.Configuration;

public class ProvisioningSettings
{
    public const string SectionName = "Provisioning";

    /// <summary>
    /// Domain for generated student sign-in addresses, e.g. hhs-2026-0007@haladehighschool.edu.
    /// The applicant's own address stays on their registration request as contact detail.
    /// </summary>
    [Required]
    [RegularExpression(
        @"^[A-Za-z0-9][A-Za-z0-9.-]*\.[A-Za-z]{2,}$",
        ErrorMessage = "StudentEmailDomain must be a bare domain such as haladehighschool.edu.")]
    public string StudentEmailDomain { get; set; } = "haladehighschool.edu";
}
