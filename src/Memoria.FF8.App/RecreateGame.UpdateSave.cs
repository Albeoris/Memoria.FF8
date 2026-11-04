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
        if (!request.Command.Equals("graphics-restart", StringComparison.OrdinalIgnoreCase) && !request.Command.Equals("save-for-update", StringComparison.OrdinalIgnoreCase) && !request.Command.Equals("cancel-update", StringComparison.OrdinalIgnoreCase) && !request.Command.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Update commands: status, save-for-update, cancel-update, graphics-restart, quit.");
        }
        return DispatchControl(request);
    }

    private SaveBoundaryState ReadSaveBoundaryState()
    {
        SaveBoundaryState state = new()
        {
            Faulted = fieldFaulted || fieldRenderFaulted || events?.LastFault is not null,
            Scene = session.State.Scene,
            StartupActive = startup.Active,
            WorldLoaded = world is not null,
            InterfaceOpen = gameMenus?.IsOpen == true || saveMenu.IsOpen || sceneInspector is not null || modelBrowser is not null || capturingControls || drainUiButtons,
            AutomaticPlayActive = autoPlay?.Running == true || autoPlayFailureHeld,
            BattleOrCardPending = ActiveBattle is not null || battleEntryTransition is not null || battleDebug is not null || world?.HasPendingBattle == true || world?.ActiveTripleTriad is not null,
            InteractionPending = world?.PendingEntityName is not null || world?.CurrentDialogue is not null || world?.Tutorial is not null
        };
        if (state.Assess() is not (SaveBoundaryStatus.FieldNotReady or SaveBoundaryStatus.WorldNotReady))
        {
            return state;
        }
        if (state.Scene == SceneKind.WorldMap)
        {
            return state with
            {
                WorldReady = world?.WorldScene is not null && session.State.World is { NativeScriptedRuntime: null, NativeTrainRuntime: null } && !world.WorldScene.IsPresentationDeferred
            };
        }
        bool fieldReady = world?.HasCurrentFieldAllocation == true && events?.NextTrigger.Kind == PlayerTriggerKind.Playable && events.CanAcceptMovementInput && !events.IsDialogueOpen && !FieldPresentationReadiness.HasPendingInitialization(events.Scene.NativeFieldInitialization);
        return state with
        {
            FieldReady = fieldReady,
            ScriptedMovementActive = fieldReady && events?.PlayerActor is { } player && (player.MovementTarget is not null || player.MovementActorId is not null || player.NativeScriptMovement is { Active: true } || player.NativeJump is { Active: true } || player.NativeLadder is { Active: true } || player.NativeLadderMode3 is { Active: true })
        };
    }

    private void EnsureUpdateSaveIsSafe()
    {
        string? failure = ReadSaveBoundaryState().Assess() switch
        {
            SaveBoundaryStatus.Faulted => "The game stopped at a script or rendering fault. Load a running save before updating.",
            SaveBoundaryStatus.NotPlaying => "Start or load a game and wait for a playable field or world boundary before updating.",
            SaveBoundaryStatus.InterfaceOpen => "Close the current menu or editor before updating so all changes belong to the saved game.",
            SaveBoundaryStatus.AutomaticPlayActive => "Stop automatic play before preparing an update save.",
            SaveBoundaryStatus.BattleOrCardPending => "Finish the current battle or card game before updating.",
            SaveBoundaryStatus.InteractionPending => "Finish the current dialogue, naming prompt or tutorial before updating.",
            SaveBoundaryStatus.FieldNotReady => "Wait until the current field event or transition returns player control before updating.",
            SaveBoundaryStatus.ScriptedMovementActive => "Finish the current scripted movement before updating.",
            SaveBoundaryStatus.WorldNotReady => "Wait until the current world event or transition finishes before updating.",
            SaveBoundaryStatus.Ready => null,
            _ => throw new InvalidOperationException("Unknown save boundary status.")
        };
        if (failure is not null)
        {
            throw new InvalidOperationException(failure);
        }
    }

    private GameState CaptureUpdateState()
    {
        // Snapshot the live VM into a separate state; validation must not mutate the running session.
        return session.PrepareSnapshot() with { DialogueDraft = null };
    }
}
