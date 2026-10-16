using Microsoft.Data.Sqlite;

namespace Acut.Data.Mapping;

/// <summary>Читання значень за назвою стовпця.</summary>
internal static class ReaderExtensions
{
    public static int GetInt(this SqliteDataReader r, string column) => r.GetInt32(r.GetOrdinal(column));
    public static long GetLong(this SqliteDataReader r, string column) => r.GetInt64(r.GetOrdinal(column));
    public static string GetStr(this SqliteDataReader r, string column) => r.GetString(r.GetOrdinal(column));
    public static double GetDbl(this SqliteDataReader r, string column) => r.GetDouble(r.GetOrdinal(column));

    public static int? GetNullableInt(this SqliteDataReader r, string column)
    {
        var ordinal = r.GetOrdinal(column);
        return r.IsDBNull(ordinal) ? null : r.GetInt32(ordinal);
    }

    public static string? GetNullableStr(this SqliteDataReader r, string column)
    {
        var ordinal = r.GetOrdinal(column);
        return r.IsDBNull(ordinal) ? null : r.GetString(ordinal);
    }

    public static TEnum GetEnum<TEnum>(this SqliteDataReader r, string column) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(r.GetStr(column));
}
