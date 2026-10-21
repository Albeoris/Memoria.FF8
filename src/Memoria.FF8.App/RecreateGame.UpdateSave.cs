using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private object DispatchUpdateControl(ControlRequest request)
    {
        if (request.Command.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            return new { session.State.Scene, UpdatePaused = updateSave.Paused };
        }
        if (!request.Command.Equals("save-for-update", StringComparison.OrdinalIgnoreCase) && !request.Command.Equals("cancel-update", StringComparison.OrdinalIgnoreCase) && !request.Command.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Update commands: status, save-for-update, cancel-update, quit.");
        }
        return DispatchControl(request);
    }

    private void EnsureUpdateSaveIsSafe()
    {
        if (fieldFaulted || fieldRenderFaulted || events?.LastFault is not null)
        {
            throw new InvalidOperationException("The game stopped at a script or rendering fault. Load a running save before updating.");
        }
        if (startup.Active || session.State.Scene is not (SceneKind.Field or SceneKind.WorldMap) || world is null)
        {
            throw new InvalidOperationException("Start or load a game and wait for a playable field or world boundary before updating.");
        }
        if (gameMenus?.IsOpen == true || saveMenu.IsOpen || sceneInspector is not null || capturingControls || drainUiButtons)
        {
            throw new InvalidOperationException("Close the current menu or editor before updating so all changes belong to the saved game.");
        }
        if (autoPlay?.Running == true || autoPlayFailureHeld)
        {
            throw new InvalidOperationException("Stop automatic play before preparing an update save.");
        }
        if (ActiveBattle is not null || battleEntryTransition is not null || battleDebug is not null || world.HasPendingBattle || world.ActiveTripleTriad is not null)
        {
            throw new InvalidOperationException("Finish the current battle or card game before updating.");
        }
        if (world.PendingEntityName is not null || world.CurrentDialogue is not null || world.Tutorial is not null)
        {
            throw new InvalidOperationException("Finish the current dialogue, naming prompt or tutorial before updating.");
        }
        if (session.State.Scene == SceneKind.Field)
        {
            if (!world.HasCurrentFieldAllocation || events?.NextTrigger.Kind != PlayerTriggerKind.Playable || !events.CanAcceptMovementInput || events.IsDialogueOpen || FieldPresentationReadiness.HasPendingInitialization(events.Scene.NativeFieldInitialization))
            {
                throw new InvalidOperationException("Wait until the current field event or transition returns player control before updating.");
            }
            if (events.PlayerActor is { } player && (player.MovementTarget is not null || player.MovementActorId is not null || player.NativeScriptMovement is { Active: true } || player.NativeJump is { Active: true } || player.NativeLadder is { Active: true } || player.NativeLadderMode3 is { Active: true }))
            {
                throw new InvalidOperationException("Finish the current scripted movement before updating.");
            }
        }
        else if (world.WorldScene is null || session.State.World is not { } currentWorld || currentWorld.NativeScriptedRuntime is not null || currentWorld.NativeTrainRuntime is not null || world.WorldScene.IsPresentationDeferred)
        {
            throw new InvalidOperationException("Wait until the current world event or transition finishes before updating.");
        }
    }

    private GameState CaptureUpdateState()
    {
        // Snapshot the live VM into a separate state; validation must not mutate the running session.
        return session.State.Scene == SceneKind.Field ? session.State with { EventState = events!.Capture(), DialogueDraft = null } : session.PrepareSnapshot();
    }
}
