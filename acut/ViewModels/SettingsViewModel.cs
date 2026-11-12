using acut.Services;

namespace acut.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(NavigationService navigation)
    {
        BackCommand = new RelayCommand(() => navigation.NavigateTo<MainMenuViewModel>());
    }

    public string Title => "Налаштування";
    public string Description => "Екран буде реалізовано на етапі 6.";
    public RelayCommand BackCommand { get; }
}
