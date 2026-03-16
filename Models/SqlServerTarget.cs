namespace SQLManager.Models;

public sealed record SqlServerTarget(
    string Key,
    string DisplayName,
    string? IpAddress,
    string? ConnectionString,
    string? ConfigurationError = null)
{
    public bool IsAvailable => string.IsNullOrWhiteSpace(ConfigurationError);

    public string DisplayLabel => string.IsNullOrWhiteSpace(IpAddress)
        ? DisplayName
        : $"{DisplayName} ({IpAddress})";
}
