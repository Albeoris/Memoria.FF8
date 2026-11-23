using System.Collections.Immutable;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    internal const string GameplayDifficultyProfilePending = "The difficulty's mod profile is not ready. Wait for mod updates or resolve the problems shown in Mods.";

    public ImmutableArray<StatusStatModifier> ReadStatusStatModifiers(BattleDifficulty difficulty)
    {
        Pump();
        if (!SetGameplayDifficulty(difficulty))
        {
            throw new InvalidOperationException(GameplayDifficultyProfilePending);
        }
        return GameplayPolicy.StatusStatModifiers;
    }

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

    internal void PrepareStartupGameplayDifficulty(BattleDifficulty difficulty)
    {
        if (SetGameplayDifficulty(difficulty))
        {
            return;
        }
        CompleteStartupPreparation(CancellationToken.None);
        if (Mods.Catalog.Hardcore != (difficulty == BattleDifficulty.Hardcore))
        {
            ModServiceView view = Mods.View;
            string diagnostics = string.Join(" ", view.Diagnostics.TakeLast(3).Select(diagnostic => diagnostic.Message));
            throw new InvalidOperationException($"Could not prepare the {difficulty} mod profile at startup ({view.Outcome}). {diagnostics}".TrimEnd());
        }
    }
}
