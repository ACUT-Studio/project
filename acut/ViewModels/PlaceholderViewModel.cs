using acut.Services;

namespace acut.ViewModels;

/// <summary>Заглушка екрана, який буде реалізовано на етапі 6.</summary>
public abstract class PlaceholderViewModel : ViewModelBase
{
    protected PlaceholderViewModel(string title, NavigationService navigation)
    {
        Title = title;
        BackCommand = new RelayCommand(() => navigation.NavigateTo<CityViewModel>());
    }

    public string Title { get; }
    public string Description => "Екран буде реалізовано на етапі 6.";
    public RelayCommand BackCommand { get; }
}
