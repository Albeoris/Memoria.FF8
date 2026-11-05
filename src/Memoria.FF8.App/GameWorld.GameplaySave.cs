using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal GameState WriteGameplaySave(string path, Action<GameState>? validateContinuation = null)
    {
        GameplaySaveComponentDocument document = CaptureGameplaySave();
        return GameplaySaveSessionWriter.Write(path, document, assets.GameDirectory, assets, resources, modelOverrides, readGameplayPolicy, validateContinuation);
    }

    /// <summary>Captures durable progress and the current normal-load world position from this live host.</summary>
    internal GameplaySaveComponentDocument CaptureGameplaySave()
    {
        if (session.State.Scene != SceneKind.WorldMap)
        {
            throw new NotSupportedException("Gameplay capture of an active field requires its logical script continuation adapter.");
        }
        if (diagnosticAllocationFaulted || worldScene is null || worldScene.IsPresentationDeferred || battle is not null || HasPendingBattle || ActiveTripleTriad is not null || CurrentDialogue is not null || PendingEntityName is not null || Events.Scene.NativeTutorial is not null || Events.Scene.NativeMenuTutorial is not null || Events.LastFault is not null)
        {
            throw new InvalidOperationException("Gameplay capture requires an allocated playable world without a pending interaction, battle or fault.");
        }
        GameState state = session.PrepareSnapshot();
        WorldState current = state.World ?? throw new InvalidOperationException("Gameplay world capture requires a current world position.");
        if (current.NativeScriptedRuntime is not null || current.NativeTrainRuntime is not null || current.EventUi is { ScriptMode: not 0 } || current.EventUi is { Windows.Length: > 0 } || current.EventUi is { ClosingChannels.Length: > 0 } || state.MoviePlayback is not null || state.PendingEntityNames is { Length: > 0 })
        {
            throw new NotSupportedException("Gameplay world capture requires a completed ordinary exploration boundary.");
        }
        (byte Entity, byte Animation, byte MapDisplay) selection = CaptureWorldSelection(current);
        GameplaySaveWorldPosition position = new(current.X, current.Z, checked((short)current.NativeHeight), checked((ushort)current.BodyYaw), selection.Entity)
        {
            AnimationVariant = selection.Animation,
            MapDisplayMode = selection.MapDisplay == 3 ? (byte)0 : selection.MapDisplay
        };
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.World, state.FieldId, state.PreviousFieldId, null, position);
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(state, resume);
        GameplaySaveSceneFacts facts = GameplaySaveSceneFacts.FromPreparedState(state) ?? throw new NotSupportedException("Gameplay capture requires current published scene facts.");
        document = document with { Progression = document.Progression! with { SceneFacts = facts } };
        document.Validate();
        return document;
    }

    private static (byte Entity, byte Animation, byte MapDisplay) CaptureWorldSelection(WorldState state)
    {
        int owners = (state.NativeFootRuntime is null ? 0 : 1) + (state.NativeCarRuntime is null ? 0 : 1) + (state.NativeGardenRuntime is null ? 0 : 1) + (state.NativeRagnarokRuntime is null ? 0 : 1);
        if (owners != 1)
        {
            throw new NotSupportedException("Gameplay world capture requires exactly one supported current controlled transport.");
        }
        if (state.NativeFootRuntime is { } foot)
        {
            return (foot.EntityType, foot.AnimationVariant, foot.MapDisplayMode);
        }
        if (state.NativeCarRuntime is { } car)
        {
            return (car.EntityType, car.AnimationVariant, car.MapDisplayMode);
        }
        if (state.NativeGardenRuntime is { } garden)
        {
            return (48, garden.AnimationVariant, garden.MapDisplayMode);
        }
        NativeRagnarokWorldRuntimeState ragnarok = state.NativeRagnarokRuntime!;
        return (50, ragnarok.AnimationVariant, ragnarok.MapDisplayMode);
    }
}
