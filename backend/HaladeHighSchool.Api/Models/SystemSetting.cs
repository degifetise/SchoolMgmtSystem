namespace HaladeHighSchool.Api.Models;

/// <summary>Admin editable key/value configuration stored in the database.</summary>
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
