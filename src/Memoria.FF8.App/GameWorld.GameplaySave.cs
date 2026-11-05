using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal GameState WriteGameplaySave(string path, Action<GameState>? validateContinuation = null, DateTime? savedAtUtc = null)
    {
        return WriteGameplaySave(path, validateContinuation, savedAtUtc, null);
    }

    internal GameState WriteGameplaySave(string path, Action<GameState>? validateContinuation, DateTime? savedAtUtc, GameplaySaveAudioPresentation? audioPresentation)
    {
        GameplaySaveComponentDocument document = CaptureGameplaySave() with { AudioPresentation = audioPresentation ?? session.State.AudioPresentation };
        GuardianForceAbilityProfile? profile = session.State.CharacterVitals?.GuardianForceProfile;
        Func<NativeCharacterVitals, NativeCharacterVitals>? prepareDefinitions;
        if (modelOverrides is GameResourceHost selected)
        {
            prepareDefinitions = initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected);
        }
        else
        {
            prepareDefinitions = profile is null ? null : initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, profile.AppliedLayers);
        }
        return GameplaySaveSessionWriter.Write(path, document, assets.GameDirectory, assets, resources, modelOverrides, readGameplayPolicy, validateContinuation, savedAtUtc, prepareDefinitions);
    }

    /// <summary>Captures supported durable progress and the current resume authority from this live host.</summary>
    internal GameplaySaveComponentDocument CaptureGameplaySave()
    {
        if (session.State.Scene == SceneKind.Field)
        {
            return Events.Frame == 0 && session.State.FieldLoadContext?.Origin == NativeFieldLoadOrigin.OriginalSave ? CaptureImportedGameplayField() : CaptureCurrentGameplayField();
        }
        if (session.State.Scene != SceneKind.WorldMap)
        {
            throw new NotSupportedException("Gameplay capture of an active field requires its logical script continuation adapter.");
        }
        if (diagnosticAllocationFaulted || worldScene is null || worldScene.IsPresentationDeferred || battle is not null || HasPendingBattle || ActiveTripleTriad is not null || PendingEntityName is not null || Events.Scene.NativeTutorial is not null || Events.Scene.NativeMenuTutorial is not null || Events.LastFault is not null)
        {
            throw new InvalidOperationException("Gameplay capture requires an allocated playable world without a pending interaction, battle or fault.");
        }
        GameState state = session.PrepareSnapshot();
        WorldState current = state.World ?? throw new InvalidOperationException("Gameplay world capture requires a current world position.");
        if (current.NativeScriptedRuntime is not null || current.NativeTrainRuntime is not null || current.EventUi is { ScriptMode: not 0 } || state.MoviePlayback is not null || state.PendingEntityNames is { Length: > 0 })
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
        return CaptureGameplayDocument(state, resume);
    }

    private GameplaySaveComponentDocument CaptureCurrentGameplayField()
    {
        bool supportedBoundary = Events.CanAcceptMovementInput || Events.HasSupportedGameplayScriptMenu || Events.HasSupportedGameplayDialogueBoundary;
        if (diagnosticAllocationFaulted || !HasCurrentFieldAllocation || battle is not null || HasPendingBattle || ActiveTripleTriad is not null || PendingEntityName is not null || Events.Scene.NativeTutorial is not null || Events.Scene.NativeMenuTutorial is not null || Events.LastFault is not null || !supportedBoundary)
        {
            throw new InvalidOperationException("Gameplay field capture requires player control, a supported dialogue or script menu without a pending battle or fault.");
        }
        GameplaySaveFieldContinuation continuation = Events.CaptureGameplaySaveFieldContinuation();
        GameState state = session.PrepareSnapshot();
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.Field, state.FieldId, state.PreviousFieldId, continuation.GetLeaderPosition(), null);
        GameplaySaveComponentDocument document = CaptureGameplayDocument(state, resume);
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        document.Validate();
        return document;
    }

    private GameplaySaveComponentDocument CaptureImportedGameplayField()
    {
        if (diagnosticAllocationFaulted || Events.LastFault is not null || !HasCurrentFieldAllocation || Events.Frame != 0 || Events.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Allocated } || session.State.FieldLoadContext is not { Origin: NativeFieldLoadOrigin.OriginalSave } context || context.FieldId != session.State.FieldId || session.State.OriginalSave is not { } original || session.State.FieldArrival is not { } arrival || arrival.FieldId != session.State.FieldId)
        {
            throw new NotSupportedException("Gameplay capture of an active field requires its logical script continuation adapter.");
        }
        GameState state = session.PrepareSnapshot();
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.Field, state.FieldId, state.PreviousFieldId, new(arrival.X, arrival.Y, arrival.Triangle, checked((byte)arrival.Direction)), null);
        GameplaySaveComponentDocument document = CaptureGameplayDocument(state, resume);
        NativeFieldLocationPose?[] poses = document.Progression!.SceneFacts!.FieldParty.Select((character, slot) => character == 255 ? null : original.ReadFieldPartyPosition(slot)).ToArray();
        document = document with { Progression = document.Progression with { FieldLocation = new(checked((ushort)state.FieldId), state.PreviousFieldId is int previous ? checked((ushort)previous) : null, poses) } };
        document.Validate();
        return document;
    }

    private static GameplaySaveComponentDocument CaptureGameplayDocument(GameState state, GameplaySaveResumeDescriptor resume)
    {
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
