using System.Windows;
using Acut.Core.Abstractions;
using Acut.Core.Services;
using Acut.Data;
using Acut.Data.Sqlite;
using acut.Services;
using acut.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace acut;

/// <summary>Composition root: тут збираються репозиторії, сервіси та ViewModel.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            _services = collection.BuildServiceProvider();

            _services.GetRequiredService<DatabaseInitializer>().Initialize();

            _services.GetRequiredService<MainWindow>().Show();
            _services.GetRequiredService<NavigationService>().NavigateTo<MainMenuViewModel>();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не вдалося запустити гру:\n{ex.Message}", "AcutStudio",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Data
        services.AddSingleton(new SqliteConnectionFactory(SqliteConnectionFactory.DefaultDatabasePath));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ICatalogRepository, SqliteCatalogRepository>();
        services.AddSingleton<ISaveRepository, SqliteSaveRepository>();

        // Core
        services.AddSingleton<SaveManager>();

        // UI-сервіси
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DialogService>();

        // ViewModel: transient, щоб кожен перехід створював свіжий екран
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<CharacterCreationViewModel>();
        services.AddTransient<CityViewModel>();
        services.AddTransient<LoadingViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<CraftingViewModel>();
        services.AddTransient<BattleViewModel>();
        services.AddTransient<VictoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddSingleton<MainWindow>();
    }
}
