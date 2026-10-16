using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

public sealed class CharacterCreationViewModel : ViewModelBase
{
    private readonly NavigationService _navigation;
    private readonly SaveManager _saves;
    private readonly ICatalogRepository _catalog;
    private readonly DialogService _dialogs;

    private string _name = string.Empty;
    private string _hairStyle = "Short";
    private string _skinColor = "Light";
    private string _outfit = "Traveler";

    public CharacterCreationViewModel(
        NavigationService navigation, SaveManager saves, ICatalogRepository catalog, DialogService dialogs)
    {
        _navigation = navigation;
        _saves = saves;
        _catalog = catalog;
        _dialogs = dialogs;

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => _navigation.NavigateTo<MainMenuViewModel>());
    }

    public string[] HairStyles { get; } = ["Short", "Long", "Braid"];
    public string[] SkinColors { get; } = ["Light", "Tan", "Dark"];
    public string[] Outfits { get; } = ["Traveler", "Merchant", "Knight"];

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string HairStyle { get => _hairStyle; set => SetProperty(ref _hairStyle, value); }
    public string SkinColor { get => _skinColor; set => SetProperty(ref _skinColor, value); }
    public string Outfit { get => _outfit; set => SetProperty(ref _outfit, value); }

    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    private void Confirm()
    {
        try
        {
            var session = GameSession.StartNewGame(Name, new AppearanceSettings(HairStyle, SkinColor, Outfit), _catalog);
            _saves.Register(session);
            session.Save(); // перше збереження: «Створення персонажа підтверджено»
            _navigation.NavigateTo(session.CurrentState);
        }
        catch (GameRuleException ex)
        {
            _dialogs.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Не вдалося створити гру:\n{ex.Message}");
        }
    }
}
