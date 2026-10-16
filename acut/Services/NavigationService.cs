using Acut.Core.Services;
using acut.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace acut.Services;

/// <summary>Перемикає екрани. Попередній ViewModel звільняється (таймери міста тощо).</summary>
public sealed class NavigationService : ViewModelBase
{
    private readonly IServiceProvider _services;
    private ViewModelBase? _current;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public ViewModelBase? CurrentViewModel
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var previous = _current;
        CurrentViewModel = _services.GetRequiredService<TViewModel>();
        (previous as IDisposable)?.Dispose();
    }

    /// <summary>Відповідність стану гри екрану. Пауза поки що веде на екран налаштувань (етап 6).</summary>
    public void NavigateTo(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu: NavigateTo<MainMenuViewModel>(); break;
            case GameState.CharacterCreation: NavigateTo<CharacterCreationViewModel>(); break;
            case GameState.City: NavigateTo<CityViewModel>(); break;
            case GameState.Battle: NavigateTo<BattleViewModel>(); break;
            case GameState.Victory:
            case GameState.Defeat: NavigateTo<VictoryViewModel>(); break;
            case GameState.Paused: NavigateTo<SettingsViewModel>(); break;
            default: throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }
}
