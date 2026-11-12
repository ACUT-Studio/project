using acut.Services;

namespace acut.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public MainViewModel(NavigationService navigation, AutoSaveService? autoSave = null)
    {
        Navigation = navigation;
        AutoSave = autoSave;
        TogglePauseCommand = new RelayCommand(
            () => (Navigation.CurrentViewModel as CityViewModel)?.TogglePauseCommand.Execute(null),
            () => Navigation.CurrentViewModel is CityViewModel);
    }

    public NavigationService Navigation { get; }
    public AutoSaveService? AutoSave { get; }
    public RelayCommand TogglePauseCommand { get; }
}
