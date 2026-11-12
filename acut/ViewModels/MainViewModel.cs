using acut.Services;

namespace acut.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public MainViewModel(NavigationService navigation)
    {
        Navigation = navigation;
        TogglePauseCommand = new RelayCommand(
            () => (Navigation.CurrentViewModel as CityViewModel)?.TogglePauseCommand.Execute(null),
            () => Navigation.CurrentViewModel is CityViewModel);
    }

    public NavigationService Navigation { get; }
    public RelayCommand TogglePauseCommand { get; }
}
