using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace acut.Converters;

/// <summary>Вибирає текстуру каталогу з ресурсів інтерфейсу, не змінюючи ігровий баланс.</summary>
public sealed class BuildingTextureConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Application.Current?.TryFindResource($"BuildingTexture{value}") as ImageSource
        ?? Application.Current?.TryFindResource("DefaultBuildingTexture") as ImageSource;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
