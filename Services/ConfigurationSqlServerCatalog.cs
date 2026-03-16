using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SQLManager.Configuration;
using SQLManager.Models;
using System.Net;
using System.Net.Sockets;

namespace SQLManager.Services;

public sealed class ConfigurationSqlServerCatalog(IOptions<SqlServerCatalogOptions> options) : ISqlServerCatalog
{
    private readonly IReadOnlyList<SqlServerTarget> _servers = BuildServers(options.Value);

    public IReadOnlyList<SqlServerTarget> GetServers() => _servers;

    public SqlServerTarget GetRequiredServer(string serverKey) =>
        _servers.FirstOrDefault(server => string.Equals(server.Key, serverKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Unknown SQL Server key '{serverKey}'.");

    private static IReadOnlyList<SqlServerTarget> BuildServers(SqlServerCatalogOptions options)
    {
        var duplicateKeys = options.Servers
            .GroupBy(server => server.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateKeys.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate SQL Server keys found: {string.Join(", ", duplicateKeys)}.");
        }

        return options.Servers.Select(BuildServer).ToArray();
    }

    private static SqlServerTarget BuildServer(SqlServerConnectionOption server)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(server.ConnectionString);
            var displayName = string.IsNullOrWhiteSpace(builder.DataSource) ? server.Key : builder.DataSource;
            var ipAddress = ResolveIpAddress(builder.DataSource);
            return new SqlServerTarget(server.Key, displayName, ipAddress, NormalizeConnectionString(builder));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return new SqlServerTarget(
                server.Key,
                server.Key,
                null,
                null,
                $"Invalid connection string in configuration: {ex.Message}");
        }
    }

    private static string NormalizeConnectionString(SqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            builder.InitialCatalog = "master";
        }

        if (!builder.ContainsKey("TrustServerCertificate"))
        {
            builder.TrustServerCertificate = true;
        }

        if (builder.ConnectTimeout <= 0)
        {
            builder.ConnectTimeout = 5;
        }

        return builder.ConnectionString;
    }

    private static string? ResolveIpAddress(string? dataSource)
    {
        var host = ExtractHost(dataSource);
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        if (IsLocalHost(host))
        {
            return ResolveLocalLanIpAddress();
        }

        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return ipAddress.ToString();
        }

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var ipv4 = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
            return (ipv4 ?? addresses.FirstOrDefault())?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveLocalLanIpAddress()
    {
        try
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            var lanAddress = addresses.FirstOrDefault(address =>
                address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address) &&
                !address.ToString().StartsWith("169.254.", StringComparison.Ordinal));

            return lanAddress?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractHost(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return null;
        }

        var source = dataSource.Trim();

        var slashIndex = source.IndexOf('\\');
        if (slashIndex >= 0)
        {
            source = source[..slashIndex];
        }

        if (source.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            source = source[4..];
        }

        if (source.StartsWith(".", StringComparison.Ordinal))
        {
            return "localhost";
        }

        if (source.StartsWith("(local)", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return "localhost";
        }

        var commaIndex = source.IndexOf(',');
        if (commaIndex >= 0)
        {
            source = source[..commaIndex];
        }

        return source.Trim();
    }

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("(local)", StringComparison.OrdinalIgnoreCase) ||
        host.Equals(".", StringComparison.Ordinal) ||
        host.Equals("127.0.0.1", StringComparison.Ordinal);
}
