using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace Acut.Data;

/// <summary>
/// Створює схему та наповнює каталог при першому запуску, застосовує міграції.
/// Версія схеми зберігається в PRAGMA user_version.
/// </summary>
public sealed class DatabaseInitializer
{
    /// <summary>Поточна версія схеми. Нова версія N додає файл Sql/migration_00N.sql.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly SqliteConnectionFactory _factory;

    public DatabaseInitializer(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public void Initialize()
    {
        using var connection = _factory.CreateConnection();
        var version = GetUserVersion(connection);

        if (version > CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"Файл БД створено новішою версією гри (схема {version}, підтримується {CurrentSchemaVersion}).");
        if (version == CurrentSchemaVersion)
            return;

        // Усе або нічого: якщо скрипт впаде, транзакція відкотиться разом із версією схеми.
        using var transaction = connection.BeginTransaction();

        if (version == 0)
        {
            Execute(connection, transaction, ReadResource("schema.sql"));
            Execute(connection, transaction, ReadResource("seed.sql"));
            version = 1;
        }
        for (var next = version + 1; next <= CurrentSchemaVersion; next++)
            Execute(connection, transaction, ReadResource($"migration_{next:D3}.sql"));

        SetUserVersion(connection, transaction, CurrentSchemaVersion);
        transaction.Commit();
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        var result = SqlHelper.Scalar(connection, null, "PRAGMA user_version;");
        return Convert.ToInt32(result);
    }

    // PRAGMA не приймає параметрів; підставляється лише ціле число з константи.
    private static void SetUserVersion(SqliteConnection connection, SqliteTransaction transaction, int version) =>
        SqlHelper.Execute(connection, transaction, $"PRAGMA user_version = {version};");

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string script) =>
        SqlHelper.Execute(connection, transaction, script);

    private static string ReadResource(string name)
    {
        using var stream = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Вбудований SQL-ресурс «{name}» не знайдено.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
