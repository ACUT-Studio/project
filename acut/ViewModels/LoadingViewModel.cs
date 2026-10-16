using acut.Services;

namespace acut.ViewModels;

public sealed class LoadingViewModel : PlaceholderViewModel
{
    public LoadingViewModel(NavigationService navigation) : base("Завантаження", navigation) { }
}
