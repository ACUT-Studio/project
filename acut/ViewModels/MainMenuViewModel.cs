using System.Windows;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

public sealed class MainMenuViewModel : ViewModelBase
{
    private readonly NavigationService _navigation;
    private readonly SaveManager _saves;
    private readonly DialogService _dialogs;

    public MainMenuViewModel(NavigationService navigation, SaveManager saves, DialogService dialogs)
    {
        _navigation = navigation;
        _saves = saves;
        _dialogs = dialogs;

        NewGameCommand = new RelayCommand(() => _navigation.NavigateTo<CharacterCreationViewModel>());
        ContinueCommand = new RelayCommand(Continue, () => CanContinue);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
    }

    public bool CanContinue => _saves.ListSaves().Count > 0;

    public RelayCommand NewGameCommand { get; }
    public RelayCommand ContinueCommand { get; }
    public RelayCommand ExitCommand { get; }

    private void Continue()
    {
        try
        {
            var latest = _saves.ListSaves().FirstOrDefault();
            if (latest is null) return;

            var session = _saves.LoadGame(latest.SaveId);
            if (session is null)
            {
                _dialogs.ShowError("Збереження не знайдено.");
                return;
            }
            _navigation.NavigateTo(session.CurrentState);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Не вдалося завантажити збереження:\n{ex.Message}");
        }
    }
}
