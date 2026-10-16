namespace Acut.Core.Services;

/// <summary>Стани гри (діаграма станів 2.1). Підстани міста — справа UI.</summary>
public enum GameState
{
    MainMenu,
    CharacterCreation,
    City,
    Battle,
    Victory,
    Defeat,
    Paused
}
