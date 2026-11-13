using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace Acut.Tests.Data;

internal sealed class TestDatabase : IDisposable
{
    public TestDatabase()
    {
        var options = new SqliteConnectionStringBuilder
        {
            DataSource = $"acut-test-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        };

        Factory = new SqliteConnectionFactory(options);
        Connection = Factory.CreateConnection();

        Initializer = new DatabaseInitializer(Factory);
        Initializer.Initialize();

        Catalog = new SqliteCatalogRepository(Factory);
        Saves = new SqliteSaveRepository(Factory, Catalog);
    }

    public SqliteConnection Connection { get; }
    public SqliteConnectionFactory Factory { get; }
    public DatabaseInitializer Initializer { get; }
    public SqliteCatalogRepository Catalog { get; }
    public SqliteSaveRepository Saves { get; }

    public int Count(string table)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int Execute(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }

    public object? Scalar(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose() => Connection.Dispose();
}