using Microsoft.Data.SqlClient;
using SQLManager.Models;

namespace SQLManager.Services;

public sealed class DatabaseAdminService(ISqlServerCatalog serverCatalog) : IDatabaseAdminService
{
    private const int SqlOperationTimeoutSeconds = 20;

    private const string DatabaseListSql = """
        SELECT
            d.name,
            d.state_desc,
            CAST(COALESCE(SUM(CAST(mf.size AS BIGINT)) * 8.0 / 1024, 0) AS decimal(18, 2)) AS size_mb
        FROM sys.databases AS d
        LEFT JOIN sys.master_files AS mf
            ON d.database_id = mf.database_id
        WHERE d.database_id > 4
        GROUP BY d.name, d.state_desc
        ORDER BY d.name;
        """;

    private const string DatabaseExistsSql = """
        SELECT COUNT(*)
        FROM sys.databases
        WHERE database_id > 4 AND name = @databaseName;
        """;

    public Task<IReadOnlyList<SqlServerTarget>> GetServersAsync() =>
        Task.FromResult(serverCatalog.GetServers());

    public async Task<IReadOnlyList<DatabaseListItem>> GetDatabasesAsync(
        string serverKey,
        CancellationToken cancellationToken = default)
    {
        var server = serverCatalog.GetRequiredServer(serverKey);
        using var operationScope = CreateTimeoutScope(cancellationToken);
        await using var connection = await OpenConnectionAsync(server, operationScope.Token);
        await using var command = CreateCommand(DatabaseListSql, connection);

        var databases = new List<DatabaseListItem>();
        await using var reader = await command.ExecuteReaderAsync(operationScope.Token);
        while (await reader.ReadAsync(operationScope.Token))
        {
            databases.Add(new DatabaseListItem(
                server.Key,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2)));
        }

        return databases;
    }

    public async Task BringOnlineAsync(
        string serverKey,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        var server = serverCatalog.GetRequiredServer(serverKey);
        using var operationScope = CreateTimeoutScope(cancellationToken);
        await EnsureUserDatabaseExistsAsync(server, databaseName, operationScope.Token);
        var sql = $"ALTER DATABASE {QuoteIdentifier(databaseName)} SET ONLINE;";
        await ExecuteNonQueryAsync(server, sql, operationScope.Token);
    }

    public async Task TakeOfflineAsync(
        string serverKey,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        var server = serverCatalog.GetRequiredServer(serverKey);
        using var operationScope = CreateTimeoutScope(cancellationToken);
        await EnsureUserDatabaseExistsAsync(server, databaseName, operationScope.Token);
        var sql = $"ALTER DATABASE {QuoteIdentifier(databaseName)} SET OFFLINE WITH ROLLBACK IMMEDIATE;";
        await ExecuteNonQueryAsync(server, sql, operationScope.Token);
    }

    private static async Task<SqlConnection> OpenConnectionAsync(SqlServerTarget server, CancellationToken cancellationToken)
    {
        if (!server.IsAvailable || string.IsNullOrWhiteSpace(server.ConnectionString))
        {
            throw new InvalidOperationException(server.ConfigurationError ?? "This SQL Server entry is not configured correctly.");
        }

        var builder = new SqlConnectionStringBuilder(server.ConnectionString)
        {
            ConnectTimeout = SqlOperationTimeoutSeconds
        };

        var connection = new SqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            await connection.DisposeAsync();
            throw new InvalidOperationException(
                $"Timed out after {SqlOperationTimeoutSeconds} seconds while connecting to '{server.DisplayName}'.",
                ex);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            await connection.DisposeAsync();
            throw new InvalidOperationException(
                $"Could not connect to '{server.DisplayName}'. Verify the server is reachable and the connection string is valid.",
                ex);
        }
    }

    private async Task EnsureUserDatabaseExistsAsync(
        SqlServerTarget server,
        string databaseName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Database name is required.");
        }

        await using var connection = await OpenConnectionAsync(server, cancellationToken);
        await using var command = CreateCommand(DatabaseExistsSql, connection);
        command.Parameters.AddWithValue("@databaseName", databaseName);

        var result = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (result <= 0)
        {
            throw new InvalidOperationException(
                $"Database '{databaseName}' was not found as a user database on server '{server.DisplayName}'.");
        }
    }

    private static async Task ExecuteNonQueryAsync(
        SqlServerTarget server,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(server, cancellationToken);
        await using var command = CreateCommand(sql, connection);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Timed out after {SqlOperationTimeoutSeconds} seconds while executing a command on '{server.DisplayName}'.",
                ex);
        }
    }

    private static SqlCommand CreateCommand(string sql, SqlConnection connection) =>
        new(sql, connection)
        {
            CommandTimeout = SqlOperationTimeoutSeconds
        };

    private static CancellationTokenSource CreateTimeoutScope(CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(SqlOperationTimeoutSeconds));
        return timeoutSource;
    }

    private static string QuoteIdentifier(string databaseName) =>
        $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
}
