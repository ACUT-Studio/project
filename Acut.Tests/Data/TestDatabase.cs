using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace Acut.Tests.Data;

/// <summary>Ізольована спільна SQLite у пам'яті; живе до завершення тесту.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private bool _disposed;

    public TestDatabase()
    {
        Factory = new SqliteConnectionFactory(new SqliteConnectionStringBuilder
        {
            DataSource = $"acut-test-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        });
        _keepAlive = Factory.CreateConnection();
        new DatabaseInitializer(Factory).Initialize();
        Catalog = new SqliteCatalogRepository(Factory);
        Saves = new SqliteSaveRepository(Factory, Catalog);
    }

    public SqliteConnectionFactory Factory { get; }
    public SqliteCatalogRepository Catalog { get; }
    public SqliteSaveRepository Saves { get; }

    public long Count(string table)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var connection = Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public bool TableExists(string tableName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var connection = Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$table";
        command.Parameters.AddWithValue("$table", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _keepAlive.Dispose();
        _disposed = true;
    }
}
