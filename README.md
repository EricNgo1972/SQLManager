# SQLManager

Internal Blazor Server tool for managing SQL Server databases across multiple servers on a local network.

## Features

- Lists configured SQL Servers from `appsettings.json`
- Shows user databases only
- Shows database status: online/offline
- Shows database size in MB
- Shows server IP next to the server name
- Allows bringing a database online
- Allows taking a database offline with `ROLLBACK IMMEDIATE`
- Handles invalid or unreachable connection strings with visible per-server errors
- Uses a static landing page at `/` and loads the Blazor dashboard at `/dashboard`

## Project Structure

- `Program.cs`: app startup and endpoint setup
- `appsettings.json`: SQL Server connection list
- `wwwroot/index.html`: static landing page
- `Components/Pages/Home.razor`: dashboard UI
- `Services/DatabaseAdminService.cs`: SQL operations
- `Services/ConfigurationSqlServerCatalog.cs`: server configuration and display metadata

## Configuration

Edit `appsettings.json`:

```json
{
  "SqlServers": {
    "Servers": [
      {
        "Key": "LocalSql",
        "ConnectionString": "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;"
      },
      {
        "Key": "FinanceSql",
        "ConnectionString": "Server=SQL-FINANCE;Database=master;Integrated Security=True;TrustServerCertificate=True;"
      }
    ]
  }
}
```

Notes:

- `Key` is the internal identifier shown in the UI
- `ConnectionString` should point to `master`
- Localhost entries will try to display the machine LAN IP instead of `127.0.0.1`

## Routes

- `/`: static splash page
- `/dashboard`: Blazor SQL management dashboard

## Run

From the project folder:

```bash
dotnet run
```

Then open:

```text
https://localhost:7197/
```

## Behavior

- SQL operations use a 20-second timeout
- Only user databases are shown
- System databases are excluded
- Offline action requires confirmation in the UI
- If a connection string is invalid or unreachable, the related server panel shows an error instead of breaking the whole page

## Requirements

- .NET 8
- Network access from the hosting machine to the configured SQL Server instances
- SQL permissions sufficient to query `sys.databases`, `sys.master_files`, and run `ALTER DATABASE`
