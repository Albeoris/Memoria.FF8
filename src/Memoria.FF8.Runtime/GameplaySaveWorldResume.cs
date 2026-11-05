namespace Memoria.FF8.Runtime;

/// <summary>Current-position presentation metadata resolved by the host from installed world resources.</summary>
public sealed record GameplaySaveWorldResumeMetadata(ushort CameraYaw, byte AnimationVariant, byte MapDisplayMode, short MapLocation)
{
    internal void Validate()
    {
        if (CameraYaw >= 4096 || MapDisplayMode > 2 || MapLocation is < sbyte.MinValue or > sbyte.MaxValue || AnimationVariant > 1)
        {
            throw new InvalidDataException("Gameplay world resume metadata is outside the supported normal-load widths.");
        }
    }
}

/// <summary>Resource-free saved-world reconstruction. It never executes a world callback or imports an original save.</summary>
public static class GameplaySaveWorldResume
{
    internal static NativeSavedWorldState RestoreSavedWorld(GameplaySaveWorldData saved, NativeSavedWorldState initialized, ushort? storyWord)
    {
        NativeSavedWorldState basis = initialized with { Origin = new(NativeSavedWorldState.CurrentProofVersion, NativeSavedWorldOriginKind.GameplaySave, NativeSavedWorldState.GameplaySaveInitializationRule) };
        NativeSavedWorldState result = saved.RestoreParkingAndFlags(basis);
        if (saved.UnresolvedEntry is not null && saved.Return is not null)
        {
            throw new InvalidDataException("Gameplay saved-world values cannot combine an unresolved entry with a supported return.");
        }
        if (saved.Return is { } stored)
        {
            if (stored.Kind == GameplaySaveWorldReturnKind.Scripted)
            {
                result = result with { DormantScriptedReturn = GameplaySaveCodec.Detach(stored) };
            }
            else
            {
                result = InstallReturn(result, stored.Kind, stored.Pose, stored.EntityType, stored.LastFieldId, storyWord ?? throw new NotSupportedException("Gameplay world restoration requires known story bytes."), stored.CameraYaw, stored.AnimationVariant ?? throw new NotSupportedException("Gameplay world restoration requires a known animation variant."), stored.MapDisplayMode ?? throw new NotSupportedException("Gameplay world restoration requires a known map display mode."), stored.MapLocation, stored.RetireSelectedVehicle ?? false);
            }
        }
        result.Validate();
        return result;
    }

    /// <summary>Completes a pending world state using its current descriptor pose and host-resolved metadata, not a stale stored return.</summary>
    public static GameState Complete(GameplaySaveComponentDocument document, GameState restored, GameplaySaveWorldResumeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(restored);
        ArgumentNullException.ThrowIfNull(metadata);
        document.Validate();
        if (document.Version != GameplaySaveComponentDocument.CurrentVersion || document.Progression?.World is null || document.Resume is not { Kind: GameplaySaveResumeKind.World, World: { } current } resume || restored.Scene != SceneKind.WorldMap || restored.FieldId != resume.FieldId || restored.World is not null || restored.WorldArrival is not null || restored.EventState is not null || restored.WorldEventState is not null || restored.DepartingFieldState is not null || restored.SavedWorld is not { Origin.Kind: NativeSavedWorldOriginKind.GameplaySave } parked || restored.WorldEntryMemory is null || restored.WorldControls is null)
        {
            throw new InvalidOperationException("Gameplay world completion requires an unallocated restored world, current resume descriptor and initialized semantic owners.");
        }
        GameplaySaveWorldReturnKind kind = Kind(current.EntityType);
        if (parked.UnsupportedEntry is not null)
        {
            throw new NotSupportedException("Gameplay world resume cannot discard unresolved saved transport values.");
        }
        metadata.Validate();
        NativeSavedWorldPose pose = new(current.X, current.Z, current.Height, current.BodyYaw);
        NativeSavedWorldState saved = InstallReturn(parked, kind, pose, current.EntityType, checked((ushort)resume.FieldId), restored.WorldEntryMemory.StoryWord, metadata.CameraYaw, metadata.AnimationVariant, metadata.MapDisplayMode, metadata.MapLocation, retireSelectedVehicle: false);
        return restored with { SavedWorld = saved, WorldArrival = new(62, current.EntityType, -1), WorldConfiguration = NativeWorldConfiguration.FromGameplaySave(current.EntityType, resume.FieldId) };
    }

    private static GameplaySaveWorldReturnKind Kind(byte entity)
    {
        return entity switch
        {
            _ when NativeWorldEntityTypes.IsSupportedFoot(entity) => GameplaySaveWorldReturnKind.Foot,
            _ when NativeCarWorldRuntimeState.IsSupportedEntity(entity) => GameplaySaveWorldReturnKind.Car,
            48 => GameplaySaveWorldReturnKind.Garden,
            50 => GameplaySaveWorldReturnKind.Ragnarok,
            _ => throw new NotSupportedException($"Gameplay normal world reload for entity {entity} has no supported semantic adapter.")
        };
    }

    private static NativeSavedWorldState InstallReturn(NativeSavedWorldState source, GameplaySaveWorldReturnKind kind, NativeSavedWorldPose pose, byte entity, ushort fieldId, ushort story, ushort camera, byte animation, byte mapDisplay, short location, bool retireSelectedVehicle)
    {
        if (Kind(entity) != kind)
        {
            throw new NotSupportedException("Gameplay world return kind differs from its supported entity.");
        }
        new GameplaySaveWorldResumeMetadata(camera, animation, mapDisplay, location).Validate();
        NativeSavedWorldState basis = source with { FootReturn = null, CarReturn = null, GardenReturn = null, RagnarokReturn = null, ScriptedReturn = null, DormantScriptedReturn = null, UnsupportedEntry = null };
        NativeSavedWorldState result;
        switch (kind)
        {
            case GameplaySaveWorldReturnKind.Foot:
                result = basis with { FootReturn = new() { ProofVersion = 1, Rule = NativeSavedWorldFootReturn.GameplaySaveRule, SourceWorldFrame = 0, LastFieldId = fieldId, StoryWord = story, WorldMode = 0, Pose0 = pose, CameraYaw96 = camera, EntityType105 = entity, Channel106 = 255, Channel107 = 255, AnimationVariant108 = animation, MapDisplayMode110 = mapDisplay, MapLocationId = location } };
                break;
            case GameplaySaveWorldReturnKind.Car:
                result = basis with { Slot4 = new(retireSelectedVehicle ? (byte)255 : NativeCarWorldRuntimeState.ShapeForEntity(entity), pose), CarReturn = new() { Version = 2, Rule = NativeSavedWorldCarReturn.GameplaySaveRule, SourceWorldFrame = 0, LastFieldId = fieldId, StoryWord = story, Pose = pose, CameraYaw96 = camera, EntityType105 = entity, Channel106 = 255, Channel107 = 255, AnimationVariant108 = animation, MapDisplayMode110 = mapDisplay, MapLocationId = location, RetireSelectedVehicle = retireSelectedVehicle } };
                break;
            case GameplaySaveWorldReturnKind.Garden:
                result = basis with { GardenPose3 = pose, GardenReturn = new(1, NativeSavedWorldGardenReturn.GameplaySaveRule, 0, fieldId, story, pose, camera, 48, 255, 255, animation, mapDisplay, location) };
                break;
            case GameplaySaveWorldReturnKind.Ragnarok:
                result = basis with { RagnarokPose2 = pose, RagnarokReturn = new(NativeSavedWorldRagnarokReturn.GameplaySaveRule, 0, fieldId, story, pose, camera, mapDisplay, location) { AnimationVariant108 = animation } };
                break;
            default:
                throw new NotSupportedException("Gameplay world transport requires a semantic load adapter.");
        }
        result.Validate();
        return result;
    }
}
