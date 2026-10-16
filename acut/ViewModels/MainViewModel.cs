using acut.Services;

namespace acut.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public MainViewModel(NavigationService navigation)
    {
        Navigation = navigation;
    }

    public NavigationService Navigation { get; }
}
