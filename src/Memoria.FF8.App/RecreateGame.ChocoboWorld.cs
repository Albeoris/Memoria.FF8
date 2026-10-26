using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class ChocoboWorldSettings
{
    internal static ChocoboWorldMode DefaultMode(GameDataEdition edition) => edition == GameDataEdition.Remastered ? ChocoboWorldMode.AngeloSearch : ChocoboWorldMode.NativeImport;

    internal static string Label(ChocoboWorldMode mode) => mode switch
    {
        ChocoboWorldMode.NativeImport => "Native Import",
        ChocoboWorldMode.AngeloSearch => "Angelo Search",
        ChocoboWorldMode.Memoria => "Memoria",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    internal static ChocoboWorldMode Parse(string label) => label switch
    {
        "Native Import" => ChocoboWorldMode.NativeImport,
        "Angelo Search" => ChocoboWorldMode.AngeloSearch,
        "Memoria" => ChocoboWorldMode.Memoria,
        _ => throw new ArgumentException("Unknown Chocobo World mode.", nameof(label))
    };

    internal static GameState Migrate(GameState state, GameDataEdition edition)
    {
        state = ChocoboRewardTransactions.Reconcile(state);
        ChocoboWorldMode mode = state.GameplayPreferences.ChocoboWorldMode ?? DefaultMode(edition);
        return ChocoboRewardTransactions.MigrateLegacyBattle(state with
        {
            GameplayPreferences = state.GameplayPreferences with { ChocoboWorldMode = mode },
            PendingGameplayPreferences = state.PendingGameplayPreferences is { } pending ? pending with { ChocoboWorldMode = pending.ChocoboWorldMode ?? mode } : null
        }, mode);
    }
}

internal sealed partial class RecreateGame
{
    private ChocoboWorldMode DefaultChocoboWorldMode => ChocoboWorldSettings.DefaultMode(resourceHost!.Resources.Source.Edition);
    private string? ChocoboWorldBoundaryUnavailableReason => ActiveBattle is not null || session.State.Battle is not null || session.State.PendingBattleEncounter is not null ? "Chocobo World changes are unavailable until the active or pending battle ends." : saveMenu.IsOpen ? "Close the save screen before changing Chocobo World mode." : session.State.PendingMagicModeReview ? "Resolve the pending gameplay settings review before changing Chocobo World mode." : null;

    private bool ApplyChocoboWorldMenuAction(MenuAction action)
    {
        if (action.Kind != "config" || action.SubjectId != "Chocobo World")
        {
            return false;
        }
        ChocoboWorldMode mode = ChocoboWorldSettings.Parse(action.Value);
        if (session.State.Scene == SceneKind.Title)
        {
            newGameMagicPreferences = newGameMagicPreferences with { ChocoboWorldMode = mode };
            return true;
        }
        if (ChocoboWorldBoundaryUnavailableReason is { } reason)
        {
            ShowSaveNotice(reason);
            return true;
        }
        session.SetGameplayPreferences((session.State.PendingGameplayPreferences ?? session.State.GameplayPreferences) with { ChocoboWorldMode = mode });
        gameMenus?.InvalidateLiveData();
        if (mode == ChocoboWorldMode.Memoria)
        {
            gameMenus?.ShowChocoboClaim();
        }
        return true;
    }

    private string? BrowseChocoboWorldSave()
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = LocalizeAuthored("chocobo.import.title", "Import Chocobo World"),
            Filter = LocalizeAuthored("chocobo.import.filter", "Native Chocobo World saves") + "|chocorpg;*.ff8;*.mcs;*.bin;*.dat|" + LocalizeAuthored("common.all-files", "All files") + "|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        try
        {
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
        finally
        {
            drainUiButtons = true;
        }
    }
}
