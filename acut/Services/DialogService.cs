using System.Windows;

namespace acut.Services;

public sealed class DialogService
{
    public void ShowError(string message) =>
        MessageBox.Show(message, "AcutStudio", MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowInfo(string message) =>
        MessageBox.Show(message, "AcutStudio", MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message) =>
        MessageBox.Show(message, "AcutStudio", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
