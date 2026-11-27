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

    internal bool CanCaptureGameplaySave => !Events.HasAbilityShop && battle is null && !session.HasActiveBattle && (session.State.Scene switch
    {
        SceneKind.Field => IsImportedGameplayField ? CanCaptureImportedGameplayField() : CanCaptureCurrentGameplayField(),
        SceneKind.WorldMap => CanCaptureGameplayWorld() && session.State.World is { } current && IsSupportedGameplayWorld(current, session.State),
        _ => false
    });

    private bool IsImportedGameplayField => Events.Frame == 0 && session.State.FieldLoadContext?.Origin == NativeFieldLoadOrigin.OriginalSave;

    private bool CanCaptureGameplayWorld()
    {
        return !diagnosticAllocationFaulted && worldScene is { IsPresentationDeferred: false } && !HasPendingBattle && ActiveTripleTriad is null && PendingEntityName is null && Events.Scene.NativeTutorial is null && Events.Scene.NativeMenuTutorial is null && Events.LastFault is null;
    }

    private bool CanCaptureCurrentGameplayField()
    {
        return !diagnosticAllocationFaulted && HasCurrentFieldAllocation && !HasPendingBattle && ActiveTripleTriad is null && PendingEntityName is null && Events.Scene.NativeTutorial is null && Events.Scene.NativeMenuTutorial is null && Events.LastFault is null && (Events.CanAcceptMovementInput || Events.HasSupportedGameplayScriptMenu || Events.HasSupportedGameplayDialogueBoundary);
    }

    private bool CanCaptureImportedGameplayField()
    {
        return !diagnosticAllocationFaulted && Events.LastFault is null && HasCurrentFieldAllocation && IsImportedGameplayField && Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Allocated } && session.State.FieldLoadContext!.FieldId == session.State.FieldId && session.State.OriginalSave is not null && session.State.FieldArrival?.FieldId == session.State.FieldId;
    }

    private static bool IsSupportedGameplayWorld(WorldState current, GameState state)
    {
        return current.DrawPoint is null && current.NativeScriptedRuntime is null && current.NativeTrainRuntime is null && current.NativeGardenRuntime?.Transfer is not { Counter: < 31 } && current.NativeRagnarokRuntime?.Transfer is null && current.EventUi is not { ScriptMode: not (0 or 14) } && state.MoviePlayback is not { IsPlaying: true } && state.PendingEntityNames is not { Length: > 0 };
    }

    /// <summary>Captures supported durable progress and the current resume authority from this live host.</summary>
    internal GameplaySaveComponentDocument CaptureGameplaySave()
    {
        if (battle is not null || session.HasActiveBattle)
        {
            throw new InvalidOperationException("Saving during battle is unavailable.");
        }
        if (session.State.Scene == SceneKind.Field)
        {
            return IsImportedGameplayField ? CaptureImportedGameplayField() : CaptureCurrentGameplayField();
        }
        if (session.State.Scene != SceneKind.WorldMap)
        {
            throw new NotSupportedException("Gameplay capture of an active field requires its logical script continuation adapter.");
        }
        if (!CanCaptureGameplayWorld())
        {
            throw new InvalidOperationException("Gameplay capture requires an allocated playable world without a pending interaction, battle or fault.");
        }
        GameState state = session.PrepareSnapshot();
        WorldState current = state.World ?? throw new InvalidOperationException("Gameplay world capture requires a current world position.");
        if (!IsSupportedGameplayWorld(current, state))
        {
            throw new NotSupportedException("Gameplay world capture requires a completed ordinary exploration boundary.");
        }
        (byte Entity, byte Animation, byte MapDisplay) selection = CaptureWorldSelection(current);
        if (current.NativeFootRuntime is not null)
        {
            state = state with { SavedWorld = GameSession.PrepareGardenParkingForFootReturn(state.SavedWorld!, current) };
        }
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
        if (!CanCaptureCurrentGameplayField())
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
        if (!CanCaptureImportedGameplayField())
        {
            throw new NotSupportedException("Gameplay capture of an active field requires its logical script continuation adapter.");
        }
        NativePcSaveData original = session.State.OriginalSave!;
        FieldTransition arrival = session.State.FieldArrival!;
        GameState state = session.PrepareSnapshot();
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.Field, state.FieldId, state.PreviousFieldId, new(arrival.X, arrival.Y, arrival.Triangle, checked((byte)arrival.Direction)), null);
        GameplaySaveComponentDocument document = CaptureGameplayDocument(state, resume);
        NativeFieldLocationPose?[] poses = state.GameplayFieldLocation?.FieldId == state.FieldId ? state.GameplayFieldLocation.PartyPositions.ToArray() : Enumerable.Range(0, 3).Select(slot => (NativeFieldLocationPose?)original.ReadFieldPartyPosition(slot)).ToArray();
        document = document with { Progression = document.Progression! with { FieldLocation = new(checked((ushort)state.FieldId), state.PreviousFieldId is int previous ? checked((ushort)previous) : null, poses) { IsNativeImport = true } } };
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
        // Garden and Ragnarok retain their parked allocation while the foot owner controls the player.
        int owners = (state.NativeFootRuntime is null ? 0 : 1) + (state.NativeCarRuntime is null ? 0 : 1) + (state.NativeFootRuntime is null && state.NativeGardenRuntime is not null ? 1 : 0) + (state.NativeFootRuntime is null && state.NativeRagnarokRuntime is not null ? 1 : 0) + (state.NativeChocoboRuntime is not null ? 1 : 0);
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
        if (state.NativeChocoboRuntime is { } chocobo)
        {
            return (49, chocobo.Objects.AnimationVariant, chocobo.MapDisplayMode);
        }
        if (state.NativeGardenRuntime is { } garden)
        {
            return (48, garden.AnimationVariant, garden.MapDisplayMode);
        }
        NativeRagnarokWorldRuntimeState ragnarok = state.NativeRagnarokRuntime!;
        return (50, ragnarok.AnimationVariant, ragnarok.MapDisplayMode);
    }
}
