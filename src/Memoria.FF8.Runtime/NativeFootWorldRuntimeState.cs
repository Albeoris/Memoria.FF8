using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Completed54B460 case8 transfer, retaining the physical Garden and camera observations before control changes to foot.</summary>
public sealed record NativeWorldGardenDismountReceipt([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] NativeGardenTransferState Transfer, [property: JsonRequired] int FootObjectIndex, [property: JsonRequired] short FootBodyYaw, [property: JsonRequired] NativeWorldFootCameraState ProjectionCamera, [property: JsonRequired] long SourceProcessCallbacks)
{
    public const string DismountRule = "ff8-en-garden-dismount-54b460-53e200-retained-process-v1";

    public void Validate(NativeGardenWorldRuntimeState? garden = null, NativeWorldProcess? process = null)
    {
        Transfer?.Validate();
        ProjectionCamera?.Validate();
        if (Version != 1 || Rule != DismountRule || Transfer is null || Transfer.Counter != 31 || Transfer.DestinationEntity != 0 || Transfer.InitialGardenPosition is null || Transfer.LastGardenPosition is null || Transfer.LastLoweringFrame is null || FootObjectIndex < 0 || ProjectionCamera is null || SourceProcessCallbacks < 0)
        {
            throw new InvalidDataException("Native Garden foot handoff requires the completed thirty-one-callback lowering and retained camera/process observations.");
        }
        if (garden is not null)
        {
            garden.Validate();
            if (FootObjectIndex != garden.Allocation.FootObjectIndex || Transfer.GardenObjectIndex != garden.Allocation.Garden64Index || garden.Objects[Transfer.GardenObjectIndex].Position != Transfer.LastGardenPosition || FootBodyYaw != garden.Objects[Transfer.GardenObjectIndex].RotationY)
            {
                throw new InvalidDataException("Native Garden dismount receipt differs from its actual parked shape64 or controlled foot allocation.");
            }
        }
        if (process is not null)
        {
            process.Validate();
            if (process.MainLoopCallbacks < SourceProcessCallbacks)
            {
                throw new InvalidDataException("Native Garden dismount cannot precede its retained main-loop process observations.");
            }
        }
    }
}

/// <summary>Executed normal-foot loader authority for native 20409E4 and C75CF8; absence in historical world snapshots remains unknown.</summary>
public sealed partial record NativeFootWorldRuntimeState
{
    public const int CurrentProofVersion = 1;
    public const string FirstLoadRule = "ff8-en-first-foot-542b90-548080-save110-v1";
    public const string SavedReloadRule = "ff8-en-foot-entry62-548080-54a0d0-v1";
    public const string TrainDismountRule = "ff8-en-foot-train-dismount-548af0-retained-process-v1";
    public const string GardenDismountRule = NativeWorldGardenDismountReceipt.DismountRule;
    public const string RagnarokDismountRule = NativeWorldRagnarokDismountReceipt.DismountRule;

    [JsonRequired]
    public required int ProofVersion { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    /// <summary>Native world entity type selected by the original world-entry packet.</summary>
    [JsonRequired]
    public required byte EntityType { get; init; }
    /// <summary>Native 20409E4, selected separately from locomotion animation IDs and their phase.</summary>
    [JsonRequired]
    public required byte AnimationVariant { get; init; }
    /// <summary>Native C75CF8 controls the world map display; native 549E80 normalizes fullscreen mode3 to saved0.</summary>
    [JsonRequired]
    public required byte MapDisplayMode { get; init; }
    /// <summary>Native camera-space203ED04 signed offsets, written by557250 or552EB0 and retained by548AF0.</summary>
    [JsonRequired]
    public required short CameraOffsetY { get; init; }
    [JsonRequired]
    public required short CameraOffsetZ { get; init; }
    [JsonRequired]
    public required long? ReloadSourceWorldFrame { get; init; }

    [JsonRequired]
    public NativeWorldGroundCacheState? GroundCache { get; init; }
    [JsonRequired]
    public int? GroundYaw { get; init; }
    [JsonRequired]
    public int? PreviousHorizontalInput { get; init; }
    [JsonRequired]
    public NativeWorldForestState? Forest { get; init; }
    /// <summary>548AF0 keeps both existing train banks and the selected section9 allocation alive.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTrainWorldRuntimeState? StationTrains { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldTrainDismountReceipt? TrainDismount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeStationTrainProcess? StationTrainProcess { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeNormalFootObjectProcess? NormalObjects { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldGardenDismountReceipt? GardenDismount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldRagnarokDismountReceipt? RagnarokDismount { get; init; }

    public static NativeFootWorldRuntimeState CreateGardenDismount(NativeGardenWorldRuntimeState garden, NativeGardenTransferState completedTransfer, NativeWorldProcess process, int? previousHorizontalInput)
    {
        ArgumentNullException.ThrowIfNull(garden);
        ArgumentNullException.ThrowIfNull(completedTransfer);
        ArgumentNullException.ThrowIfNull(process);
        garden.Validate();
        completedTransfer.Validate();
        process.Validate();
        if (garden.Transfer != completedTransfer || completedTransfer.Counter != 31 || completedTransfer.DestinationEntity != 0 || garden.Forest.CameraCooldown != process.CameraCooldown || garden.CameraFollowSpeed != process.CarCameraFollowSpeed)
        {
            throw new InvalidOperationException("Native foot handoff requires this Garden's completed actual transfer and current shared process owners.");
        }
        NativeWorldFootCameraState camera = garden.ProjectionCamera ?? throw new NotSupportedException("Native Garden dismount requires its retained actual projection camera.");
        var receipt = new NativeWorldGardenDismountReceipt(1, GardenDismountRule, completedTransfer, garden.Allocation.FootObjectIndex, garden.Objects[garden.Allocation.Garden64Index].RotationY, camera, process.MainLoopCallbacks);
        receipt.Validate(garden, process);
        //54B460 resets animation variant and53E200 clears only the twelve cache entries.
        //It does not execute53F310/555B20/557250 or replace current/previous polygons.
        NativeWorldGroundCacheState cache = garden.GroundCache with { Alternatives = garden.GroundCache.Alternatives.Select(alternative => alternative with { Entries = [] }).ToArray() };
        var state = new NativeFootWorldRuntimeState
        {
            ProofVersion = CurrentProofVersion,
            Rule = GardenDismountRule,
            EntityType = 0,
            AnimationVariant = 0,
            MapDisplayMode = garden.MapDisplayMode,
            CameraOffsetY = camera.OffsetY,
            CameraOffsetZ = camera.OffsetZ,
            ReloadSourceWorldFrame = null,
            GroundCache = cache,
            GroundYaw = garden.GroundYaw,
            PreviousHorizontalInput = previousHorizontalInput,
            Forest = garden.Forest,
            GardenDismount = receipt
        };
        state.RequireNativeGroundState();
        state.RequireSupportedNormalFoot();
        return state;
    }

    public static NativeFootWorldRuntimeState CreateTrainDismount(NativeTrainWorldRuntimeState train, NativeWorldTrainStation station, short cameraPitch)
    {
        NativeWorldTrainDismountReceipt receipt = NativeWorldTrainDismountReceipt.Create(train, station);
        if (station.Index != 0 || train.Primary.TrackIndex != 0)
        {
            throw new NotSupportedException("Native train dismount currently represents the reached track0/station0 continuation.");
        }
        //548AF0 changes entity/control/position and clears body orientation. It does not
        //run53E200/53F310/557250, so ground selection, random process and camera survive.
        var state = new NativeFootWorldRuntimeState
        {
            ProofVersion = CurrentProofVersion,
            Rule = TrainDismountRule,
            EntityType = train.ControlledObjectEntityType,
            AnimationVariant = 0,
            MapDisplayMode = train.Load.MapDisplayMode,
            CameraOffsetY = 0,
            CameraOffsetZ = train.CameraGeometry.OffsetZ,
            ReloadSourceWorldFrame = null,
            GroundCache = train.GroundCache,
            GroundYaw = train.GroundYaw,
            PreviousHorizontalInput = 0,
            Forest = train.Forest with { CameraPitch = cameraPitch },
            StationTrains = train,
            TrainDismount = receipt,
            StationTrainProcess = NativeStationTrainProcess.Create(train)
        };
        state.RequireNativeGroundState();
        state.RequireSupportedNormalFoot();
        return state;
    }

    public static NativeFootWorldRuntimeState CreateFirstNormalLoad(NativeSavedWorldState savedWorld, byte entityType = 0)
    {
        ArgumentNullException.ThrowIfNull(savedWorld);
        byte mapDisplayMode = savedWorld.ReadMapDisplayModeForFreshFootLoader();
        var state = new NativeFootWorldRuntimeState
        {
            ProofVersion = CurrentProofVersion,
            Rule = FirstLoadRule,
            EntityType = entityType,
            AnimationVariant = 0,
            MapDisplayMode = mapDisplayMode,
            CameraOffsetY = -341,
            CameraOffsetZ = -7168,
            ReloadSourceWorldFrame = null,
            GroundCache = null,
            GroundYaw = 0,
            PreviousHorizontalInput = null
        };
        state.Validate();
        return state;
    }

    public static NativeFootWorldRuntimeState CreateSavedReload(NativeSavedWorldFootReturn receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        receipt.RequireCompleteFootReload();
        var state = new NativeFootWorldRuntimeState
        {
            ProofVersion = CurrentProofVersion,
            Rule = SavedReloadRule,
            EntityType = receipt.EntityType105,
            AnimationVariant = receipt.AnimationVariant108!.Value,
            MapDisplayMode = receipt.MapDisplayMode110!.Value,
            CameraOffsetY = -341,
            CameraOffsetZ = -7168,
            ReloadSourceWorldFrame = receipt.SourceWorldFrame,
            GroundCache = null,
            GroundYaw = 0,
            PreviousHorizontalInput = null
        };
        state.Validate();
        return state;
    }

    public void Validate()
    {
        Forest?.Validate();
        GroundCache?.Validate();
        StationTrains?.Validate();
        NormalObjects?.Validate();
        if (Rule == RagnarokDismountRule)
        {
            if (RagnarokDismount is null || GardenDismount is not null || StationTrains is not null || TrainDismount is not null || StationTrainProcess is not null || NormalObjects is not null || ReloadSourceWorldFrame is not null || EntityType != 0 || AnimationVariant != 0 || CameraOffsetY != RagnarokDismount.ProjectionCamera.OffsetY || CameraOffsetZ != RagnarokDismount.Camera.OffsetZ || GroundCache is null || GroundYaw is null || Forest is null)
            {
                throw new InvalidDataException("Ragnarok foot handoff lost its completed landing or retained ground, forest and camera owners.");
            }
            RagnarokDismount.Validate();
        }
        else if (RagnarokDismount is not null)
        {
            throw new InvalidDataException("Native foot cannot acquire a Ragnarok dismount receipt without its executed completion rule.");
        }
        else if (Rule == GardenDismountRule)
        {
            if (GardenDismount is null || GardenDismount.ProjectionCamera is null || StationTrains is not null || TrainDismount is not null || StationTrainProcess is not null || NormalObjects is not null || ReloadSourceWorldFrame is not null || EntityType != 0 || AnimationVariant != 0 || CameraOffsetY != GardenDismount.ProjectionCamera.OffsetY || CameraOffsetZ != GardenDismount.ProjectionCamera.OffsetZ || GroundCache is null || GroundYaw is null || Forest is null)
            {
                throw new InvalidDataException("Native Garden foot handoff lost its completed transfer or retained ground/forest/camera owners.");
            }
            GardenDismount.Validate();
        }
        else if (GardenDismount is not null)
        {
            throw new InvalidDataException("Native foot cannot acquire a Garden dismount receipt without its executed completion rule.");
        }
        else if (Rule == TrainDismountRule)
        {
            if (NormalObjects is not null || StationTrains is null || TrainDismount is null || StationTrainProcess is null || ReloadSourceWorldFrame is not null || EntityType != StationTrains.ControlledObjectEntityType || AnimationVariant != 0 || MapDisplayMode != StationTrains.Load.MapDisplayMode || CameraOffsetY != 0 || CameraOffsetZ != StationTrains.CameraGeometry.OffsetZ)
            {
                throw new InvalidDataException("Native foot dismount lost its retained train allocation or executed station transition.");
            }
            TrainDismount.Validate(StationTrains);
            StationTrainProcess.Validate(StationTrains);
        }
        else if (StationTrains is not null || TrainDismount is not null || StationTrainProcess is not null)
        {
            throw new InvalidDataException("Native foot loader cannot acquire station trains without an executed dismount.");
        }
        else if (CameraOffsetY != -341 || CameraOffsetZ != -7168)
        {
            throw new InvalidDataException("Native ordinary foot loader lost its original camera-space offsets.");
        }
        if (NormalObjects is not null && EntityType != 0)
        {
            throw new InvalidDataException("Native normal object allocation requires its actual controlled shape0 body.");
        }
        if (PreviousHorizontalInput is < -1 or > 1 || GroundYaw is < -1120 or > 1120)
        {
            throw new InvalidDataException("Native foot retained input/yaw exceeds its physical word contract.");
        }
        if (ProofVersion != CurrentProofVersion || Rule is not (FirstLoadRule or SavedReloadRule or TrainDismountRule or GardenDismountRule or RagnarokDismountRule) || EntityType > 1 || AnimationVariant > 1 || MapDisplayMode > 3 || Rule == FirstLoadRule && (AnimationVariant != 0 || ReloadSourceWorldFrame is not null) || Rule == SavedReloadRule && (ReloadSourceWorldFrame is null or < 0))
        {
            throw new InvalidDataException("Invalid native foot-world loader or saved-reload authority.");
        }
    }

    public void RequireNativeGroundState()
    {
        Validate();
        if (GroundCache is null || GroundYaw is null || Forest is null)
        {
            throw new NotSupportedException("Native foot movement requires executed ground-cache, selected-yaw and forest presentation authority; migrate this save explicitly.");
        }
    }

    public void RequireSupportedNormalFoot()
    {
        Validate();
        if (AnimationVariant != 0 || MapDisplayMode != 0)
        {
            throw new NotSupportedException("The native foot renderer/control path currently requires variant0 and map display mode0.");
        }
    }
}
