using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

public sealed record AppearanceOption(string Value, string Label);

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

        ConfirmCommand = new RelayCommand(Confirm, () => NameError is null);
        CancelCommand = new RelayCommand(() => _navigation.NavigateTo<MainMenuViewModel>());
    }

    public AppearanceOption[] HairStyles { get; } =
        [new("Short", "Коротка"), new("Long", "Довга"), new("Braid", "Коса")];
    public AppearanceOption[] SkinColors { get; } =
        [new("Light", "Світлий"), new("Tan", "Засмаглий"), new("Dark", "Темний")];
    public AppearanceOption[] Outfits { get; } =
        [new("Traveler", "Мандрівник"), new("Merchant", "Торговець"), new("Knight", "Лицар")];

    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value)) return;
            OnPropertyChanged(nameof(NameError));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }
    public string? NameError => GameSession.GetPlayerNameError(Name);
    public AppearanceSettings PreviewAppearance => new(HairStyle, SkinColor, Outfit);
    public string HairStyle
    {
        get => _hairStyle;
        set { if (SetProperty(ref _hairStyle, value)) OnPropertyChanged(nameof(PreviewAppearance)); }
    }
    public string SkinColor
    {
        get => _skinColor;
        set { if (SetProperty(ref _skinColor, value)) OnPropertyChanged(nameof(PreviewAppearance)); }
    }
    public string Outfit
    {
        get => _outfit;
        set { if (SetProperty(ref _outfit, value)) OnPropertyChanged(nameof(PreviewAppearance)); }
    }

    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    private void Confirm()
    {
        if (NameError is not null) return;
        try
        {
            var session = GameSession.StartNewGame(Name, PreviewAppearance, _catalog);
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
