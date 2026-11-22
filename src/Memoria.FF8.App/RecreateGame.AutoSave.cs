using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly AutoSaveScheduler autoSave = new();

    private static AutoSaveLocation? ReadAutoSaveLocation(GameState state)
    {
        return state.Scene switch
        {
            SceneKind.Field => new(state.FieldId, FieldLoad: state.FieldLoadContext),
            SceneKind.WorldMap => new(-1),
            _ => null
        };
    }

    private void UpdateAutoSaveRequest(TimeSpan elapsed)
    {
        if (session.State.Scene == SceneKind.Title)
        {
            autoSave.Reset();
            return;
        }

        autoSave.Update(uiPreferences, ReadAutoSaveLocation(session.State), gameMenus?.IsOpen == true || saveMenu.IsOpen ? TimeSpan.Zero : elapsed);
    }

    private void TryAutoSave()
    {
        if (autoSave.Pending is not null && !PlayerSaveFaulted && world?.CanCaptureGameplaySave == true)
        {
            autoSave.TrySave(WriteAutoSave);
        }
    }

    private void WriteAutoSave(AutoSaveKind kind)
    {
        PreparePlayerSlotSave();
        playerSaves.SaveAuto(kind, uiPreferences.AutoSaveCount, session, ValidatePlayerState, WritePlayerGameplaySave);
    }
}
