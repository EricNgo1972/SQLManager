using SQLManager.Models;

namespace SQLManager.Services;

public interface ISqlServerCatalog
{
    IReadOnlyList<SqlServerTarget> GetServers();

    SqlServerTarget GetRequiredServer(string serverKey);
}
