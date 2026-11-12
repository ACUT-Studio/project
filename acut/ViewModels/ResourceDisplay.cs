using Acut.Core.Domain.Economy;

namespace acut.ViewModels;

/// <summary>Локалізоване відображення ресурсів без правил економіки в XAML.</summary>
public static class ResourceDisplay
{
    public static string Format(ResourceAmounts amounts)
    {
        var parts = new List<string>();
        if (amounts.Gold > 0) parts.Add($"{amounts.Gold} золота");
        if (amounts.Wood > 0) parts.Add($"{amounts.Wood} дерева");
        if (amounts.Stone > 0) parts.Add($"{amounts.Stone} каменю");
        return parts.Count == 0 ? "нічого" : string.Join(", ", parts);
    }
}
