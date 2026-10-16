using Microsoft.Data.Sqlite;

namespace Acut.Data.Sqlite;

/// <summary>Короткі обгортки над ADO.NET: параметри завжди передаються окремо від тексту SQL.</summary>
internal static class SqlHelper
{
    public static SqliteCommand CreateCommand(
        SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    public static int Execute(
        SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql, parameters);
        return command.ExecuteNonQuery();
    }

    public static object? Scalar(
        SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql, parameters);
        var result = command.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    /// <summary>Виконує запит і викликає <paramref name="onRow"/> для кожного рядка.</summary>
    public static void Read(
        SqliteConnection connection, string sql, Action<SqliteDataReader> onRow,
        params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, null, sql, parameters);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            onRow(reader);
    }

    public static List<T> Query<T>(
        SqliteConnection connection, string sql, Func<SqliteDataReader, T> map,
        params (string Name, object? Value)[] parameters)
    {
        var result = new List<T>();
        Read(connection, sql, r => result.Add(map(r)), parameters);
        return result;
    }
}
