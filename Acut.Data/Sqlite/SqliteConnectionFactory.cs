using Microsoft.Data.Sqlite;

namespace Acut.Data.Sqlite;

/// <summary>Створює з'єднання з SQLite та вмикає зовнішні ключі (вони вимкнені за замовчуванням).</summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Шлях до файлу БД не задано.", nameof(databasePath));

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public string DatabasePath { get; }

    /// <summary>%LocalAppData%\AcutStudio\acut.db</summary>
    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AcutStudio", "acut.db");

    /// <summary>Відкрите з'єднання з PRAGMA foreign_keys = ON. Закриває той, хто викликав.</summary>
    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
