using Microsoft.Data.Sqlite;

namespace JobRadarLocal.Data;

public sealed class Database
{
    private readonly AppPaths _paths;

    public Database(AppPaths paths)
    {
        _paths = paths;
    }

    public SqliteConnection OpenConnection()
    {
        _paths.EnsureDirectories();
        var connection = new SqliteConnection($"Data Source={_paths.DatabasePath}");
        connection.Open();
        return connection;
    }
}
