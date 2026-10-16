using System.Globalization;
using Acut.Core.Domain.Characters;
using Microsoft.Data.Sqlite;

namespace Acut.Data.Mapping;

/// <summary>Перетворення станів гри між доменом і рядками таблиць збереження.</summary>
internal static class SaveMapper
{
    public static Player ReadPlayer(SqliteDataReader r) => new(
        r.GetStr("Name"),
        new AppearanceSettings(r.GetStr("HairStyle"), r.GetStr("SkinColor"), r.GetStr("Outfit")),
        level: r.GetInt("Level"),
        experience: r.GetInt("Experience"),
        gold: r.GetInt("Gold"),
        health: r.GetInt("Health"),
        maxHealth: r.GetInt("MaxHealth"),
        attackPower: r.GetInt("AttackPower"),
        defense: r.GetInt("Defense"));

    /// <summary>Дата й час: ISO 8601, завжди UTC.</summary>
    public static string FormatDate(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    public static long ToCents(decimal price) =>
        (long)Math.Round(price * 100m, MidpointRounding.AwayFromZero);

    public static decimal FromCents(long cents) => cents / 100m;
}
