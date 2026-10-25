using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal static class NewGameSettings
{
    internal static readonly string[] Ids = ["Difficulty", "Battle ATB mode", "Chocobo World", .. GameplayPreferences.NewGameOptions.Select(option => option.Label), "Rare card markers"];

    internal static bool IsLocked(string key, BattleDifficulty difficulty) => difficulty == BattleDifficulty.Hardcore && (key == "Difficulty" || GameplayPreferences.NewGameOptions.Any(option => option.Label == key));

    internal static UiPreferences Normalize(UiPreferences preferences)
    {
        if (preferences.Difficulty != BattleDifficulty.Hardcore)
        {
            return preferences;
        }
        UiPreferences normalized = preferences with { };
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions)
        {
            typeof(UiPreferences).GetProperty(option.PropertyName)?.SetValue(normalized, true);
        }
        return normalized;
    }
}
