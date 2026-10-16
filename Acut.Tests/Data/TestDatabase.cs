using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace Acut.Tests.Data;

/// <summary>Тимчасова БД у файлі з уже застосованими схемою та каталогом. Видаляється після тесту.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"acut-test-{Guid.NewGuid():N}.db");

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

    public long Count(string table)
    {
        using var connection = Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools(); // інакше файл лишається заблокованим (Windows)
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm", _path + "-journal" })
        {
            try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { }
        }
    }
}
