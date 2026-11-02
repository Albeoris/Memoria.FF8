using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal static class NewGameSettings
{
    internal static readonly string[] Ids = ["Difficulty", "Battle ATB mode", "Chocobo World", .. GameplayPreferences.NewGameOptions.Select(option => option.Label), "Rare card markers", .. UiPreferences.TripleTriadSettingKeys];

    internal static bool IsLocked(string key, BattleDifficulty difficulty, bool newGame = false) => difficulty == BattleDifficulty.Hardcore && (key == "Difficulty" && !newGame || GameplayPreferences.NewGameOptions.Any(option => option.RequiredOnHardcore && option.Label == key));

    internal static UiPreferences Normalize(UiPreferences preferences)
    {
        preferences = preferences with { TripleTriad = (preferences.TripleTriad ?? new()).NormalizeForDifficulty(preferences.Difficulty) };
        if (preferences.Difficulty != BattleDifficulty.Hardcore)
        {
            return preferences;
        }
        UiPreferences normalized = preferences with { };
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions.Where(option => option.RequiredOnHardcore))
        {
            typeof(UiPreferences).GetProperty(option.PropertyName)?.SetValue(normalized, true);
        }
        return normalized;
    }
}
