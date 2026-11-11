using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    internal const string GameplayDifficultyProfilePending = "The difficulty's mod profile is not ready. Wait for mod updates or resolve the problems shown in Mods.";

    public bool SetGameplayDifficulty(BattleDifficulty difficulty)
    {
        bool hardcore = difficulty == BattleDifficulty.Hardcore;
        bool changed = Mods.SetHardcoreMode(hardcore);
        if (Mods.Catalog.Hardcore != hardcore)
        {
            Pump(changed);
        }
        return Mods.Catalog.Hardcore == hardcore;
    }
}
