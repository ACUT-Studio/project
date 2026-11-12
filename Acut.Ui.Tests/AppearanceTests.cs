using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Acut.Core.Abstractions;
using Acut.Core.Domain.Characters;
using Acut.Core.Services;
using Acut.Tests.Core;
using acut.Services;
using acut.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Acut.Ui.Tests;

public sealed class AppearanceTests
{
    [Fact]
    public void AppearanceChoices_ShowUkrainianLabelsKeepStoredValuesAndNotifyPreview() => RunSta(() =>
    {
        using var services = CreateServices(out _);
        var vm = services.GetRequiredService<CharacterCreationViewModel>();
        Assert.Equal(new[] { "Short", "Long", "Braid" }, vm.HairStyles.Select(o => o.Value));
        Assert.Equal(new[] { "Light", "Tan", "Dark" }, vm.SkinColors.Select(o => o.Value));
        Assert.Equal(new[] { "Traveler", "Merchant", "Knight" }, vm.Outfits.Select(o => o.Value));
        Assert.Equal(new[] { "Коротка", "Довга", "Коса" }, vm.HairStyles.Select(o => o.Label));
        Assert.Equal(new[] { "Світлий", "Засмаглий", "Темний" }, vm.SkinColors.Select(o => o.Label));
        Assert.Equal(new[] { "Мандрівник", "Торговець", "Лицар" }, vm.Outfits.Select(o => o.Label));
        Assert.Equal(AppearanceSettings.Default, vm.PreviewAppearance);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        vm.HairStyle = "Long";
        Assert.Equal(new AppearanceSettings("Long", "Light", "Traveler"), vm.PreviewAppearance);
        vm.SkinColor = "Tan";
        Assert.Equal(new AppearanceSettings("Long", "Tan", "Traveler"), vm.PreviewAppearance);
        vm.Outfit = "Merchant";
        Assert.Equal(new AppearanceSettings("Long", "Tan", "Merchant"), vm.PreviewAppearance);
        Assert.Equal(3, notifications.Count(n => n == nameof(vm.PreviewAppearance)));
    });

    [Theory]
    [InlineData("Short", "Light", "Traveler")]
    [InlineData("Long", "Tan", "Merchant")]
    [InlineData("Braid", "Dark", "Knight")]
    public void Confirm_UsesPreviewAppearanceForSavedPlayerCityAndInventory(string hair, string skin, string outfit) => RunSta(() =>
    {
        using var services = CreateServices(out var repository);
        var vm = services.GetRequiredService<CharacterCreationViewModel>();
        vm.Name = "Марко";
        vm.HairStyle = hair;
        vm.SkinColor = skin;
        vm.Outfit = outfit;
        var expected = vm.PreviewAppearance;

        vm.ConfirmCommand.Execute(null);

        var session = Assert.IsType<GameSession>(services.GetRequiredService<SaveManager>().Current);
        Assert.Equal(expected, session.Player.Appearance);
        Assert.Equal(expected, repository.SavedAppearance);
        using var city = Assert.IsType<CityViewModel>(services.GetRequiredService<NavigationService>().CurrentViewModel);
        Assert.Equal(expected, city.Appearance);
        var inventory = services.GetRequiredService<InventoryViewModel>();
        Assert.Equal(expected, inventory.Appearance);
    });

    private static ServiceProvider CreateServices(out AppearanceSaveRepository repository)
    {
        repository = new AppearanceSaveRepository();
        return new ServiceCollection().AddSingleton<ICatalogRepository>(new FakeCatalog())
            .AddSingleton(new SaveManager(repository)).AddSingleton<NavigationService>().AddSingleton<DialogService>()
            .AddTransient<CharacterCreationViewModel>().AddTransient<CityViewModel>().AddTransient<InventoryViewModel>()
            .BuildServiceProvider();
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class AppearanceSaveRepository : ISaveRepository
    {
        public AppearanceSettings? SavedAppearance { get; private set; }
        public void Save(GameSession session) => SavedAppearance = session.Player.Appearance;
        public GameSession? Load(Guid saveId) => throw new NotSupportedException();
        public IReadOnlyList<SaveSlotInfo> ListSaves() => [];
        public void Delete(Guid saveId) => throw new NotSupportedException();
    }
}
