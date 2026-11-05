using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native ordinary car state from548080/557A90, independent of normal foot animation.</summary>
public sealed record NativeCarWorldRuntimeState
{
    public const string LoadRule = "ff8-en-ordinary-car-544860-548080-v4";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required byte EntityType { get; init; }
    [JsonRequired]
    public required byte ShapeId { get; init; }
    public byte AnimationVariant { get; init; }
    public int ManualCameraVelocity { get; init; }
    public NativeFootCameraTransition? CameraTransition { get; init; }
    public bool? PriorTerrainOcclusion { get; init; }
    [JsonRequired]
    public required int Speed { get; init; }
    [JsonRequired]
    public required int CameraFollowSpeed { get; init; }
    [JsonRequired]
    public required int TerrainTiltBlend { get; init; }
    [JsonRequired]
    public required int ModelTilt { get; init; }
    [JsonRequired]
    public required byte MapDisplayMode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldGroundCacheState? GroundCache { get; init; }
    public int? GroundYaw { get; init; }
    [JsonRequired]
    public int? MovementMagnitude20409F4 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTrainWorldAllocation? Allocation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldFootCameraState? ProjectionCamera { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeNormalFootObjectProcess? ObjectProcess { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeGardenWorldRuntimeState? GardenObjects { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeRagnarokWorldRuntimeState? RagnarokObjects { get; init; }
    [JsonIgnore]
    public byte FootEntityType => checked((byte)(GardenObjects is { } garden ? garden.Objects[garden.Allocation.FootObjectIndex].EntityType : RagnarokObjects is not null ? 0 : Allocation is { } allocation ? allocation.Objects[allocation.ControlledObjectIndex].EntityType : 1));
    public ImmutableArray<NativeTrainWorldObject> AppendedObjects { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? AllocationAnimationCounter { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? AllocationReplacementFlags264 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? AllocationGuardianForce13Flags { get; init; }
    [JsonIgnore]
    public ImmutableArray<NativeTrainWorldObject> AllocatedObjects => Allocation is null ? AppendedObjects : Allocation.Objects.AddRange(AppendedObjects);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldCarBoardingReceipt? Boarding { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldForestState? Forest { get; init; }

    // Older snapshots retain unknown contacts until the next physical callback.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PrimaryContact { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BroadContact { get; init; }

    public static NativeCarWorldRuntimeState Create(NativeSavedWorldState saved, NativeWorldGroundCacheState cache, NativeWorldProcess process, byte entityType = 33, NativeTrainWorldAllocation? allocation = null, NativeWorldFootCameraState? projectionCamera = null, NativeGardenWorldRuntimeState? gardenObjects = null, NativeRagnarokWorldRuntimeState? ragnarokObjects = null)
    {
        saved.Validate();
        cache.Validate();
        process.Validate();
        byte shape = ShapeForEntity(entityType);
        var result = new NativeCarWorldRuntimeState { Version = 4, Rule = LoadRule, EntityType = entityType, ShapeId = shape, Speed = 0, CameraFollowSpeed = process.CarCameraFollowSpeed, TerrainTiltBlend = 0, ModelTilt = 0, MapDisplayMode = saved.ReadMapDisplayModeForFreshFootLoader(), ManualCameraVelocity = process.VehicleManualCameraVelocity, GroundCache = cache, GroundYaw = 0, MovementMagnitude20409F4 = 0, Forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown), Allocation = allocation, ProjectionCamera = projectionCamera, GardenObjects = gardenObjects, RagnarokObjects = ragnarokObjects };
        result.Validate();
        return result;
    }

    public static bool IsSupportedEntity(int entityType)
    {
        return entityType is >= 32 and <= 40;
    }

    public static byte ShapeForEntity(byte entityType)
    {
        return entityType switch
        {
            32 => 74,
            33 => 73,
            34 => 81,
            35 => 82,
            36 => 83,
            37 => 84,
            38 => 85,
            39 => 86,
            40 => 87,
            _ => throw new NotSupportedException("Native ordinary car requires its actual supported entity type.")
        };
    }

    public void Validate()
    {
        Boarding?.Validate(this);
        if (ManualCameraVelocity is < -128 or > 128 || AnimationVariant > 1 || Version != 4 || Rule != LoadRule || !IsSupportedEntity(EntityType) || ShapeId != ShapeForEntity(EntityType) || Speed is < -64 or > 64 || CameraFollowSpeed is < 0 or > 128 || TerrainTiltBlend is < 0 or > 64 || ModelTilt is < -128 or > 192 || MapDisplayMode > 3 || GroundCache is null || GroundYaw is null or < -1120 or > 1120 || MovementMagnitude20409F4 is null or < 0 or > 64)
        {
            throw new InvalidDataException("Unsupported native ordinary car runtime state.");
        }
        CameraTransition?.Validate();
        if (CameraTransition is not null && (Forest is null || PriorTerrainOcclusion is null))
        {
            throw new InvalidDataException("Native car camera transition requires retained pitch and terrain occlusion owners.");
        }
        GroundCache.Validate();
        Forest?.Validate();
        if (GardenObjects is null && RagnarokObjects is null && (EntityType != 33 && (Allocation is null || ProjectionCamera is null) || EntityType == 33 && ObjectProcess is null && (Allocation is not null || ProjectionCamera is not null && CameraTransition is null)))
        {
            throw new InvalidDataException("Native car object allocation differs from its executed loader contract.");
        }
        GardenObjects?.Validate();
        if (GardenObjects is { } garden && (RagnarokObjects is not null || Allocation is not null || ObjectProcess is not null || !AppendedObjects.IsEmpty || ProjectionCamera is null || garden.Allocation.SavedCarIndex < 0 || garden.Objects[garden.Allocation.SavedCarIndex].ShapeId != ShapeId))
        {
            throw new InvalidDataException("Car selected from Garden must retain its sole original dense allocation and selected vehicle.");
        }
        RagnarokObjects?.Validate();
        if (RagnarokObjects is { } ragnarok && (GardenObjects is not null || Allocation is not null || ObjectProcess is not null || !AppendedObjects.IsEmpty || ProjectionCamera is null || ragnarok.Allocation.SavedCarIndex < 0 || ragnarok.Objects[ragnarok.Allocation.SavedCarIndex].ShapeId != ShapeId))
        {
            throw new InvalidDataException("Car selected from Ragnarok must retain its sole original dense allocation and selected vehicle.");
        }
        int maximumContact = GardenObjects is { } objects ? objects.Objects.Length - 1 + (objects.BackgroundRails?.Primary is not null || objects.BackgroundRails?.Alternate is not null ? 1 : 0) : RagnarokObjects is { } ragnarokObjects ? ragnarokObjects.Objects.Length - 1 + (ragnarokObjects.BackgroundRails?.HasBanks == true ? 1 : 0) : (Allocation?.Objects.Length ?? 0) + (AppendedObjects.IsDefault ? 0 : AppendedObjects.Length) - 1 + (ObjectProcess?.Primary is not null || ObjectProcess?.Alternate is not null || GardenObjects?.BackgroundRails?.HasBanks == true || RagnarokObjects?.BackgroundRails?.HasBanks == true ? 1 : 0);
        int selectedCarIndex = GardenObjects?.Allocation.SavedCarIndex ?? RagnarokObjects?.Allocation.SavedCarIndex ?? (Allocation is not null && !AppendedObjects.IsDefaultOrEmpty ? Allocation.Objects.Length : -1);
        if (selectedCarIndex >= 0 && (PrimaryContact == selectedCarIndex || BroadContact == selectedCarIndex))
        {
            throw new InvalidDataException("Native selected-car queries cannot retain their own skipped vehicle as a contact.");
        }
        if ((PrimaryContact is null) != (BroadContact is null) || PrimaryContact < -1 || BroadContact < -1 || PrimaryContact > maximumContact || BroadContact > maximumContact || PrimaryContact >= 0 && BroadContact != PrimaryContact)
        {
            throw new InvalidDataException("Native car contacts differ from their executed allocation and hard/broad query order.");
        }
        if (AppendedObjects.IsDefault || (AllocationReplacementFlags264 is null) != (AllocationGuardianForce13Flags is null) || AllocationReplacementFlags264 is null && !AppendedObjects.IsEmpty)
        {
            throw new InvalidDataException("Native car appended allocation lost its original input owners.");
        }
        if (AllocationReplacementFlags264 is { } appendFlags && AllocationGuardianForce13Flags is { } guardianFlags)
        {
            List<byte> shapes = [ShapeId];
            if ((guardianFlags & 1) == 0)
            {
                shapes.Add(94);
            }
            for (int group = 0; group < 3; group++)
            {
                if ((appendFlags & (1 << group)) != 0)
                {
                    shapes.Add(79);
                    shapes.Add(80);
                }
            }
            if (Allocation is null || !AppendedObjects.Select(item => item.ShapeId).SequenceEqual(shapes) || Allocation.Objects.Length + AppendedObjects.Length > 64)
            {
                throw new InvalidDataException("Native car appended allocation differs from544860's original object order.");
            }
        }
        foreach (NativeTrainWorldObject item in AppendedObjects)
        {
            item.Validate();
            if (item.Flags != (item.ShapeId == 80 ? 4 : 0))
            {
                throw new InvalidDataException("Native car appended object animation mode differs from its original loader.");
            }
        }
        ObjectProcess?.ValidateCar();
        if (ObjectProcess is not null && (Allocation is null || ProjectionCamera is null || ObjectProcess.Allocation.ProgramHash != Allocation.ProgramHash || ObjectProcess.Allocation.CoordinateHash != Allocation.CoordinateHash || ObjectProcess.Allocation.Story != Allocation.Story || ObjectProcess.Allocation.EventFlags103 != Allocation.EventFlags103 || !ObjectProcess.Allocation.ClauseWords.SequenceEqual(Allocation.ClauseWords) || ObjectProcess.Allocation.Objects.Length != Allocation.Objects.Length || ObjectProcess.Allocation.Objects.Where((item, index) => item.Flags != Allocation.Objects[index].Flags || item.RawOperand != Allocation.Objects[index].RawOperand || !item.CoordinateBytes.SequenceEqual(Allocation.Objects[index].CoordinateBytes)).Any()))
        {
            throw new InvalidDataException("Native car object process differs from its retained dense allocation or projection owner.");
        }
        Allocation?.Validate();
        ProjectionCamera?.Validate();
    }
}

/// <summary>Executed549E80(mode1) car save and vehicle shape retirement; historical foot bytes are not reused.</summary>
public sealed record NativeSavedWorldCarReturn
{
    public bool RetireSelectedVehicle { get; init; } = true;

    public const string SaveRule = "ff8-en-car-field-return-549e80-mode1-v2";
    public const string OriginalSaveRule = "ff8-original-save-car-position-v1";
    public const string GameplaySaveRule = "ff8-gameplay-save-car-position-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required long SourceWorldFrame { get; init; }
    [JsonRequired]
    public required ushort LastFieldId { get; init; }
    [JsonRequired]
    public required ushort StoryWord { get; init; }
    [JsonRequired]
    public required NativeSavedWorldPose Pose { get; init; }
    [JsonRequired]
    public required ushort CameraYaw96 { get; init; }
    [JsonRequired]
    public required byte EntityType105 { get; init; }
    [JsonRequired]
    public required byte Channel106 { get; init; }
    [JsonRequired]
    public required byte Channel107 { get; init; }
    [JsonRequired]
    public required byte AnimationVariant108 { get; init; }
    [JsonRequired]
    public required byte MapDisplayMode110 { get; init; }
    [JsonRequired]
    public required short MapLocationId { get; init; }

    public static NativeSavedWorldCarReturn Create(WorldState world, ushort lastField, ushort story, short location)
    {
        world.NativeCarRuntime?.Validate();
        if (world.NativeCarRuntime is null)
        {
            throw new NotSupportedException("A car save requires executed native car runtime provenance.");
        }
        var result = new NativeSavedWorldCarReturn { Version = 2, Rule = SaveRule, SourceWorldFrame = world.Frame, LastFieldId = lastField, StoryWord = story, Pose = new NativeSavedWorldPose(world.X, world.Z, checked((short)world.NativeHeight), checked((ushort)world.BodyYaw)), CameraYaw96 = checked((ushort)world.CameraYaw), EntityType105 = world.NativeCarRuntime.EntityType, Channel106 = 255, Channel107 = 255, AnimationVariant108 = world.NativeCarRuntime.AnimationVariant, MapDisplayMode110 = world.NativeCarRuntime.MapDisplayMode == 3 ? (byte)0 : world.NativeCarRuntime.MapDisplayMode, MapLocationId = location };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Version != 2 || Rule is not (SaveRule or OriginalSaveRule or GameplaySaveRule) || SourceWorldFrame < 0 || Rule == GameplaySaveRule && SourceWorldFrame != 0 || Pose is null || Pose.X is < -131072 or >= 131072 || Pose.RawZ is < -98304 or >= 98304 || Pose.Yaw >= 4096 || CameraYaw96 >= 4096 || !NativeCarWorldRuntimeState.IsSupportedEntity(EntityType105) || Channel106 != 255 || Channel107 != 255 || AnimationVariant108 > 1 || MapDisplayMode110 > 2 || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new InvalidDataException("Invalid native car-to-field save receipt.");
        }
    }
}
