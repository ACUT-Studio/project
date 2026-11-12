using System.Windows.Threading;
using Acut.Core.Services;
using acut.ViewModels;

namespace acut.Services;

/// <summary>Періодичне збереження запускається UI; Core не містить таймерів.</summary>
public sealed class AutoSaveService : ViewModelBase, IDisposable
{
    private readonly SaveManager _saves;
    private readonly DispatcherTimer _timer;
    private string _statusMessage = string.Empty;

    public AutoSaveService(SaveManager saves)
    {
        _saves = saves;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private void OnTick(object? sender, EventArgs e) => SaveIfPlaying();

    public void SaveIfPlaying()
    {
        if (_saves.Current?.CurrentState is not (GameState.City or GameState.Battle)) return;
        try { _saves.AutoSave(); StatusMessage = string.Empty; }
        catch (Exception ex) { StatusMessage = $"Помилка автозбереження: {ex.Message}"; }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
