using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace Acut.Tests.Data;

/// <summary>Тимчасова БД у файлі з уже застосованими схемою та каталогом. Видаляється після тесту.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"acut-test-{Guid.NewGuid():N}.db");
    private bool _disposed;

    public TestDatabase()
    {
        Factory = new SqliteConnectionFactory(_path);
        new DatabaseInitializer(Factory).Initialize();
        Catalog = new SqliteCatalogRepository(Factory);
        Saves = new SqliteSaveRepository(Factory, Catalog);
    }

    public SqliteConnectionFactory Factory { get; }
    public SqliteCatalogRepository Catalog { get; }
    public SqliteSaveRepository Saves { get; }

    /// <summary>Підраховує кількість рядків у вказаній таблиці.</summary>
    public long Count(string table)
    {
        EnsureNotDisposed();
        using var connection = Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    /// <summary>Перевіряє, чи існує таблиця в базі даних.</summary>
    public bool TableExists(string tableName)
    {
        EnsureNotDisposed();
        using var connection = Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName;";
        command.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TestDatabase));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        SqliteConnection.ClearAllPools();

        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm", _path + "-journal" })
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (IOException)
            {
               
            }
        }

        _disposed = true;
    }
}