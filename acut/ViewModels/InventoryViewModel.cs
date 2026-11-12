using System.Collections.ObjectModel;
using System.Windows.Input;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Characters;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

public sealed record InventorySlot(int Index, ItemStack? Stack, bool IsSelected)
{
    public bool IsOccupied => Stack is not null;
    public string Hint => Stack is null ? "Порожня комірка" : $"{Stack.Item.Name} · кількість: {Stack.Quantity}";
}

/// <summary>Інвентар і спорядження поточної сесії. Місто зупинене, доки відкрито екран.</summary>
public sealed class InventoryViewModel : ViewModelBase
{
    private readonly GameSession _session;
    private readonly NavigationService _navigation;
    private readonly bool _resumeOnBack;
    private string _statusMessage = string.Empty;
    private ItemStack? _selectedItem;

    public InventoryViewModel(NavigationService navigation, SaveManager saves)
    {
        _navigation = navigation;
        _session = saves.Current ?? throw new InvalidOperationException("Гру не розпочато.");
        _resumeOnBack = _session.CurrentState == GameState.City;
        if (_resumeOnBack) _session.ChangeState(GameState.Paused);
        BackCommand = new RelayCommand(Back);
        SelectItemCommand = new RelayCommand(p => SelectItem(p as InventorySlot),
            p => p is InventorySlot { IsOccupied: true });
        EquipCommand = new RelayCommand(p => Equip(p as Item), p => CanEquip(p as Item));
        UnequipWeaponCommand = new RelayCommand(() => Unequip(true), () => IsCityPaused && EquippedWeapon is not null);
        UnequipArmorCommand = new RelayCommand(() => Unequip(false), () => IsCityPaused && EquippedArmor is not null);
        Refresh();
    }

    public ObservableCollection<ItemStack> Items { get; } = new();
    public ObservableCollection<InventorySlot> Slots { get; } = new();
    public ItemStack? SelectedItem => _selectedItem;
    public bool HasSelection => SelectedItem is not null;
    public bool IsEquipmentSelected => SelectedItem?.Item is Weapon or Armor;
    public bool IsSelectedEquipped => SelectedItem is not null &&
        (SelectedItem.Item.Id == EquippedWeapon?.Id || SelectedItem.Item.Id == EquippedArmor?.Id);
    public string EquipButtonText => IsSelectedEquipped ? "Вдягнуто" : "Вдягнути";
    public string SelectedItemStats => SelectedItem?.Item switch
    {
        Weapon weapon => $"Атака: +{weapon.BonusAttack}",
        Armor armor => $"Захист: +{armor.BonusDefense}",
        Potion potion => $"Сила ефекту: {potion.EffectValue}",
        _ => string.Empty
    };
    public Weapon? EquippedWeapon => _session.Player.EquippedWeapon;
    public Armor? EquippedArmor => _session.Player.EquippedArmor;
    public string EquippedWeaponName => EquippedWeapon?.Name ?? "Меч не вдягнуто";
    public string EquippedArmorName => EquippedArmor?.Name ?? "Броню не вдягнуто";
    public string PlayerName => _session.Player.Name;
    public AppearanceSettings Appearance => _session.Player.Appearance;
    public int PlayerLevel => _session.Player.Level;
    public string PlayerHealth => $"{_session.Player.Health} / {_session.Player.MaxHealth}";
    public int PlayerAttack => _session.Player.TotalAttackPower;
    public int PlayerDefense => _session.Player.TotalDefense;
    public int Capacity => _session.Player.Inventory.Capacity;
    public int UsedSlots => Items.Count;
    public bool IsEmpty => Items.Count == 0;
    public bool IsCityPaused => _session.CurrentState == GameState.Paused;
    public string PauseMessage => IsCityPaused ? "Місто на паузі. Ресурси не нараховуються, доки відкрито інвентар." : string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public RelayCommand EquipCommand { get; }
    public RelayCommand UnequipWeaponCommand { get; }
    public RelayCommand UnequipArmorCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand SelectItemCommand { get; }

    private void Back()
    {
        if (_resumeOnBack && IsCityPaused) _session.ChangeState(GameState.City);
        _navigation.NavigateTo<CityViewModel>();
    }

    private bool CanEquip(Item? item) => IsCityPaused && item is Weapon or Armor &&
        _session.Player.Inventory.HasItem(item) && item.Id != EquippedWeapon?.Id && item.Id != EquippedArmor?.Id;

    private void Equip(Item? item)
    {
        if (!CanEquip(item)) return;
        if (_session.Player.EquipItem(item!)) StatusMessage = $"Вдягнуто: {item!.Name}.";
        Refresh();
    }

    private void Unequip(bool weapon)
    {
        if (!IsCityPaused) return;
        if (weapon) _session.Player.UnequipWeapon();
        else _session.Player.UnequipArmor();
        StatusMessage = weapon ? "Меч знято." : "Броню знято.";
        Refresh();
    }

    private void SelectItem(InventorySlot? slot)
    {
        if (slot?.Stack is null) return;
        _selectedItem = _session.Player.Inventory.GetItems().FirstOrDefault(s => s.Item.Id == slot.Stack.Item.Id);
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedItemStats));
        OnPropertyChanged(nameof(IsEquipmentSelected));
        OnPropertyChanged(nameof(IsSelectedEquipped));
        OnPropertyChanged(nameof(EquipButtonText));
        for (var i = 0; i < Capacity; i++)
        {
            var stack = i < Items.Count ? Items[i] : null;
            var slot = new InventorySlot(i, stack, stack is not null && stack.Item.Id == SelectedItem?.Item.Id);
            if (i == Slots.Count) Slots.Add(slot);
            else if (Slots[i] != slot) Slots[i] = slot;
        }
    }

    private void Refresh()
    {
        Items.Clear();
        foreach (var stack in _session.Player.Inventory.GetItems()) Items.Add(stack);
        if (_selectedItem is not null) _selectedItem = Items.FirstOrDefault(s => s.Item.Id == _selectedItem.Item.Id);
        RefreshSelection();
        OnPropertyChanged(nameof(EquippedWeapon));
        OnPropertyChanged(nameof(EquippedArmor));
        OnPropertyChanged(nameof(EquippedWeaponName));
        OnPropertyChanged(nameof(EquippedArmorName));
        OnPropertyChanged(nameof(PlayerAttack));
        OnPropertyChanged(nameof(PlayerDefense));
        OnPropertyChanged(nameof(UsedSlots));
        OnPropertyChanged(nameof(IsEmpty));
        CommandManager.InvalidateRequerySuggested();
    }
}
