using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Acut.Core.Domain.Combat;
using acut.ViewModels;

namespace acut.Views;

public partial class BattleView : UserControl
{
    private readonly Dictionary<string, CroppedBitmap[]> _playerFramesCache = new();
    private readonly CroppedBitmap[]? _enemyAttackFrames;
    private bool _isPlayerAttacking;
    private bool _isEnemyAttacking;

    public BattleView()
    {
        InitializeComponent();

        // Безпечно підвантажуємо фон бою з будь-якої з можливих папок
        BattleBackgroundImage.Source = LoadSingleImage(
            "pack://application:,,,/Assets/Battle/battle-background.png",
            "pack://application:,,,/Assets/City/battle-background.png",
            "pack://application:,,,/Assets/battle-background.png");

        // Завантажуємо 3-кадровий спрайт-шит ворога
        _enemyAttackFrames = LoadSpriteSheet("pack://application:,,,/Assets/Enemies/Sprite-0001-sheet.png", 3);
        if (_enemyAttackFrames is not null)
        {
            EnemyPortraitImage.Source = _enemyAttackFrames[0];
        }
    }

    private static BitmapImage? LoadSingleImage(params string[] packUris)
    {
        foreach (var uri in packUris)
        {
            try
            {
                return new BitmapImage(new Uri(uri, UriKind.Absolute));
            }
            catch
            {
                // Пробуємо наступний шлях
            }
        }
        return null;
    }

    private CroppedBitmap[]? GetCurrentPlayerAttackFrames()
    {
        int outfit = 0;
        int skin = 1;

        if (DataContext is BattleViewModel vm && vm.Appearance is { } appearance)
        {
            outfit = appearance.Outfit.ToString().Equals("Merchant", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            skin = appearance.SkinColor.ToString() switch
            {
                "Light" => 1,
                "Tan" => 2,
                "Dark" => 3,
                _ => 1
            };
        }

        string fileName = $"outfit_{outfit}-skin_{skin}-attack.png";

        if (_playerFramesCache.TryGetValue(fileName, out var cachedFrames))
            return cachedFrames;

        var loaded = LoadSpriteSheet($"pack://application:,,,/Assets/Characters/{fileName}", 3);
        if (loaded is not null)
        {
            _playerFramesCache[fileName] = loaded;
            return loaded;
        }

        return null;
    }

    private static CroppedBitmap[]? LoadSpriteSheet(string packUri, int frameCount)
    {
        try
        {
            var sheet = new BitmapImage(new Uri(packUri, UriKind.Absolute));
            int frameWidth = sheet.PixelWidth / frameCount;
            int frameHeight = sheet.PixelHeight;

            var frames = new CroppedBitmap[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                var rect = new Int32Rect(i * frameWidth, 0, frameWidth, frameHeight);
                frames[i] = new CroppedBitmap(sheet, rect);
            }
            return frames;
        }
        catch
        {
            return null;
        }
    }

    private async void AttackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlayerAttacking || _isEnemyAttacking || !IsLoaded)
            return;

        await PlayPlayerAttackAsync();
        await TriggerEnemyCounterAttackAsync();
    }

    private async void TurnActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlayerAttacking || _isEnemyAttacking || !IsLoaded)
            return;

        await Task.Delay(250);
        await TriggerEnemyCounterAttackAsync();
    }

    private async Task PlayPlayerAttackAsync()
    {
        if (!IsLoaded || _isPlayerAttacking)
            return;

        _isPlayerAttacking = true;
        try
        {
            if (FindName("PlayerMoveTransform") is not null &&
                Resources["PlayerAttackStoryboard"] is Storyboard storyboard)
            {
                storyboard.Begin(this, true);
            }

            var frames = GetCurrentPlayerAttackFrames();
            if (frames is null)
                return;

            PlayerAppearance.Visibility = Visibility.Collapsed;
            PlayerAttackSprite.Visibility = Visibility.Visible;

            PlayerAttackSprite.Source = frames[1];
            await Task.Delay(80);
            if (!IsLoaded) return;

            PlayerAttackSprite.Source = frames[2];
            await Task.Delay(160);
            if (!IsLoaded) return;

            PlayerAttackSprite.Source = frames[0];
            await Task.Delay(80);
        }
        finally
        {
            if (IsLoaded)
            {
                PlayerAttackSprite.Visibility = Visibility.Collapsed;
                PlayerAppearance.Visibility = Visibility.Visible;
            }
            _isPlayerAttacking = false;
        }
    }

    private async Task TriggerEnemyCounterAttackAsync()
    {
        if (!IsLoaded)
            return;

        if (DataContext is BattleViewModel vm &&
            (vm.EnemyHealth <= 0 || vm.PlayerHealth <= 0 || vm.Battle.State == BattleState.Finished))
        {
            return;
        }

        if (EnemyHealthBar.Value <= 0)
            return;

        await Task.Delay(120);
        if (!IsLoaded)
            return;

        await PlayEnemyAttackAsync();
    }

    private async Task PlayEnemyAttackAsync()
    {
        if (!IsLoaded || _isEnemyAttacking)
            return;

        _isEnemyAttacking = true;
        try
        {
            if (FindName("EnemyMoveTransform") is not null &&
                Resources["EnemyAttackStoryboard"] is Storyboard storyboard)
            {
                storyboard.Begin(this, true);
            }

            if (_enemyAttackFrames is null)
                return;

            EnemyPortraitImage.Visibility = Visibility.Collapsed;
            EnemyAttackSprite.Visibility = Visibility.Visible;

            EnemyAttackSprite.Source = _enemyAttackFrames[1];
            await Task.Delay(90);
            if (!IsLoaded) return;

            EnemyAttackSprite.Source = _enemyAttackFrames[2];
            await Task.Delay(150);
            if (!IsLoaded) return;

            EnemyAttackSprite.Source = _enemyAttackFrames[0];
            await Task.Delay(80);
        }
        finally
        {
            if (IsLoaded)
            {
                EnemyAttackSprite.Visibility = Visibility.Collapsed;
                EnemyPortraitImage.Visibility = Visibility.Visible;
            }
            _isEnemyAttacking = false;
        }
    }
}