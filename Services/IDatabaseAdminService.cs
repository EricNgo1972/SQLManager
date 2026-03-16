using SQLManager.Models;

namespace SQLManager.Services;

public interface IDatabaseAdminService
{
    Task<IReadOnlyList<SqlServerTarget>> GetServersAsync();

    Task<IReadOnlyList<DatabaseListItem>> GetDatabasesAsync(string serverKey, CancellationToken cancellationToken = default);

    Task BringOnlineAsync(string serverKey, string databaseName, CancellationToken cancellationToken = default);

    Task TakeOfflineAsync(string serverKey, string databaseName, CancellationToken cancellationToken = default);
}
