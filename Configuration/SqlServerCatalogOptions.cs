using System.ComponentModel.DataAnnotations;

namespace SQLManager.Configuration;

public sealed class SqlServerCatalogOptions
{
    public const string SectionName = "SqlServers";

    [Required]
    public List<SqlServerConnectionOption> Servers { get; init; } = [];
}

public sealed class SqlServerConnectionOption
{
    [Required]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}
