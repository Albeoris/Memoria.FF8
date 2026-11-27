using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeGardenReboardCamera([property: JsonRequired] int ProjectionDistance, [property: JsonRequired] short OffsetY, [property: JsonRequired] short OffsetZ);

public sealed record NativeGardenWorldObject([property: JsonRequired] byte ShapeId, [property: JsonRequired] byte Flags, [property: JsonRequired] NativeWorldPathPosition Position, [property: JsonRequired] short RotationX, [property: JsonRequired] short RotationY, [property: JsonRequired] short RotationZ)
{
    public int EntityType => NativeTrainWorldObject.ClassifyShape(ShapeId);
}

public sealed record NativeGardenCameraDefaults([property: JsonRequired] string ExecutableHash, [property: JsonRequired] short Pitch, [property: JsonRequired] short OffsetX, [property: JsonRequired] short OffsetY, [property: JsonRequired] short OffsetZ)
{
    public void Validate()
    {
        if (ExecutableHash is not (NativeFieldRuntimeProfile.FF8EnExecutableHash or NativeFieldRuntimeProfile.RemasteredEnImageHash) || Pitch != -112 || OffsetX != 0 || OffsetY != -341 || OffsetZ != -7168)
        {
            throw new NotSupportedException("Garden camera words differ from the authenticated557250/558C20 ordinary projection contract.");
        }
    }
}

/// <summary>Executed544860/548080 allocation after the fresh loader's dominating548300 pose publication.</summary>
public sealed record NativeGardenWorldAllocation([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string CoordinateHash, [property: JsonRequired] string ClauseHash, [property: JsonRequired] int ClauseIndex, [property: JsonRequired] ushort MinimumStory, [property: JsonRequired] ushort MaximumStory, [property: JsonRequired] ushort Story, [property: JsonRequired] byte Flags265, [property: JsonRequired] byte ChocoboFlags1536, [property: JsonRequired] ImmutableArray<ushort> ClauseWords, [property: JsonRequired] ImmutableArray<NativeGardenWorldObject> Objects, [property: JsonRequired] int FootObjectIndex, [property: JsonRequired] int Garden64Index, [property: JsonRequired] int Garden65Index, [property: JsonRequired] WorldTransition Packet, [property: JsonRequired] NativeSavedWorldPose LoadPose, [property: JsonRequired] NativeSavedWorldPose GardenPose, [property: JsonRequired] ushort CameraYaw, [property: JsonRequired] byte AnimationVariant, [property: JsonRequired] byte MapDisplayMode)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldVehicleSlot? SavedCarSlot { get; init; }
    [JsonIgnore]
    public int SavedCarIndex => Objects.Select((item, index) => (item, index)).Where(pair => NativeCarWorldRuntimeState.IsSupportedEntity(pair.item.EntityType)).Select(pair => pair.index).DefaultIfEmpty(-1).Single();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldFootReturn? BattleReload { get; init; }
    [JsonIgnore]
    public byte ControlledEntityType => BattleReload?.EntityType105 ?? unchecked((byte)Packet.Mode);

    public const string AllocationRule = "ff8-en-garden-544860-548080-548300-v1";
    [JsonRequired]
    public byte ReplacementFlags264 { get; init; }
    [JsonRequired]
    public byte GuardianForce13Flags { get; init; }
    [JsonRequired]
    public NativeWorldPathPosition CactuarLoadPosition { get; init; }
    [JsonIgnore]
    public int CactuarIndex => Objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId == 94).Select(pair => pair.index).DefaultIfEmpty(-1).Single();

    [JsonIgnore]
    public bool HasGarden => Garden64Index >= 0;
    [JsonIgnore]
    public byte RiderEntityType => checked((byte)Objects[FootObjectIndex].EntityType);
    [JsonIgnore]
    public int MountIndex => FindShape(2);
    [JsonIgnore]
    public int CompanionIndex => FindShape(3);
    [JsonIgnore]
    public int RagnarokIndex => FindShape(1);

    private int FindShape(byte shape)
    {
        for (int index = 0; index < Objects.Length; index++)
        {
            if (Objects[index].ShapeId == shape)
            {
                return index;
            }
        }
        return -1;
    }

    public static ReadOnlySpan<ushort> ReadObjectInstructions(ImmutableArray<ushort> words, ushort minimum, ushort maximum, bool mounted)
    {
        ReadOnlySpan<ushort> header = [0xFF01, 0, 0xFF02, minimum, 0xFF03, maximum, 0xFF04, 0];
        ReadOnlySpan<ushort> clause = words.AsSpan();
        if (clause.Length < 12 || (clause.Length & 1) != 0 || !clause[..header.Length].SequenceEqual(header) || clause[^2] != 0xFF05 || clause[^1] != 0)
        {
            throw new NotSupportedException("World allocation requires its original simple-range clause and terminal instruction.");
        }
        ReadOnlySpan<ushort> records = clause[header.Length..^2];
        if (!mounted)
        {
            ReadOnlySpan<ushort> ordinary = [0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 64, 0xFF13, 65];
            if (!records.SequenceEqual(ordinary) && !(records.Length == ordinary.Length + 2 && records[..^2].SequenceEqual(ordinary) && records[^2] == 0xFF13 && records[^1] == 0x1958))
            {
                throw new NotSupportedException("Garden allocation requires its complete original ordinary object contract.");
            }
        }
        for (int index = 0; index < records.Length; index += 2)
        {
            ushort operand = records[index + 1];
            byte shape = unchecked((byte)operand);
            bool characterOrVehicle = shape is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 64 or 65;
            bool staticObject = shape >= 64 && shape is not (64 or 65 or 94) && !NativeCarWorldRuntimeState.IsSupportedEntity(NativeTrainWorldObject.ClassifyShape(shape));
            if (records[index] != 0xFF13 || !(characterOrVehicle && operand == shape || staticObject && (operand >> 8) < 128))
            {
                throw new NotSupportedException("World allocation contains an unrepresented object instruction.");
            }
        }
        return records;
    }

    public void Validate()
    {
        if (BattleReload is { } battle)
        {
            battle.RequireCompleteFootReload();
            if (battle.WorldMode != 3 || battle.EntityType105 != 0 || battle.Pose0 != LoadPose || battle.CameraYaw96 != CameraYaw || battle.MapDisplayMode110 != MapDisplayMode)
            {
                throw new InvalidDataException("World mode2 allocation differs from its actual mode3 foot save.");
            }
        }
        static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
        if (Version != 1 || Rule != AllocationRule || !Hash(ProgramHash) || !Hash(CoordinateHash) || !Hash(ClauseHash) || ClauseIndex < 0 || MinimumStory >= MaximumStory || Story < MinimumStory || Story >= MaximumStory || ClauseWords.IsDefaultOrEmpty || Objects.IsDefaultOrEmpty || Objects.Length > 64 || Objects.Any(item => item is null) || Packet is null || ControlledEntityType is not (0 or 48 or 49) && !NativeCarWorldRuntimeState.IsSupportedEntity(ControlledEntityType) || (ControlledEntityType == 48 || NativeCarWorldRuntimeState.IsSupportedEntity(ControlledEntityType)) && unchecked((sbyte)Packet.Extra) != -1 || LoadPose is null || LoadPose.X is < -131072 or >= 131072 || LoadPose.RawZ is < -98304 or >= 98304 || LoadPose.Yaw >= 4096 || GardenPose is null || GardenPose.X is < -131072 or >= 131072 || GardenPose.RawZ is < -98304 or >= 98304 || GardenPose.Yaw >= 4096 || CameraYaw >= 4096 || AnimationVariant > 1 || MapDisplayMode > 3)
        {
            throw new InvalidDataException("Native Garden allocation lacks its original clause, exact loader packet or current pose authority.");
        }
        bool mounted = ControlledEntityType == 49;
        ReadOnlySpan<ushort> records = ReadObjectInstructions(ClauseWords, MinimumStory, MaximumStory, mounted);
        List<byte> expected = [];
        for (int index = 0; index < records.Length; index += 2)
        {
            byte shape = unchecked((byte)records[index + 1]);
            if (shape != 3 || (ChocoboFlags1536 & 3) == 1)
            {
                expected.Add(shape);
            }
        }
        int footIndex = expected.FindIndex(shape => NativeWorldEntityTypes.IsSupportedFoot(NativeTrainWorldObject.ClassifyShape(shape)));
        int garden64 = expected.IndexOf(64);
        int garden65 = expected.IndexOf(65);
        if (footIndex < 0 || mounted && !expected.Contains(2) || !mounted && garden64 < 0 || (garden64 < 0) != (garden65 < 0))
        {
            throw new NotSupportedException("World allocation lacks the controller's original rider, mount or vehicle objects.");
        }
        if (expected.Contains(92) && SavedCarSlot is not null)
        {
            throw new InvalidDataException("Native shape92 clears the saved-car selection before the vehicle append.");
        }
        if (SavedCarSlot is { } car)
        {
            if (!NativeCarWorldRuntimeState.IsSupportedEntity(NativeTrainWorldObject.ClassifyShape(car.ShapeId)) || car.Pose is null || car.Pose.X is < -131072 or >= 131072 || car.Pose.RawZ is < -98304 or >= 98304 || car.Pose.Yaw >= 4096)
            {
                throw new InvalidDataException("Garden allocation lost its appended saved car shape and pose authority.");
            }
            expected.Add(car.ShapeId);
        }
        if ((GuardianForce13Flags & 1) == 0)
        {
            expected.Add(94);
        }
        for (int group = 0; group < 3; group++)
        {
            if ((ReplacementFlags264 & (1 << group)) != 0)
            {
                expected.Add(79);
                expected.Add(80);
            }
        }
        if (!Objects.Select(item => item.ShapeId).SequenceEqual(expected) || FootObjectIndex != footIndex || Garden64Index != garden64 || Garden65Index != garden65 || Objects.Any(item => item.Flags != (item.ShapeId == 65 ? 2 : item.ShapeId == 80 ? 4 : 0)))
        {
            throw new InvalidDataException("Garden dense allocation differs from its exact1536 conditional admission and original object flags.");
        }
        if (ControlledEntityType == 49)
        {
            NativeWorldPathPosition rider = new(LoadPose.X, LoadPose.Height, -LoadPose.RawZ);
            short riderYaw = unchecked((short)LoadPose.Yaw);
            NativeGardenWorldObject RiderBody(byte shape) => new(shape, 0, rider, 0, riderYaw, 0);
            if (Objects[FootObjectIndex] != RiderBody(Objects[FootObjectIndex].ShapeId) || Objects[MountIndex] != RiderBody(2) || CompanionIndex >= 0 && Objects[CompanionIndex] != RiderBody(3))
            {
                throw new InvalidDataException("Mounted allocation lost the original rider and mount publication.");
            }
            if (HasGarden)
            {
                NativeWorldPathPosition garden = new(GardenPose.X, GardenPose.Height, -GardenPose.RawZ);
                short gardenYaw = unchecked((short)GardenPose.Yaw);
                if (Objects[Garden64Index] != new NativeGardenWorldObject(64, 0, garden, 0, gardenYaw, 0) || Objects[Garden65Index] != new NativeGardenWorldObject(65, 2, garden, 0, gardenYaw, 0))
                {
                    throw new InvalidDataException("Mounted allocation lost the independently parked Garden pose.");
                }
            }
        }
        if (CactuarIndex >= 0 && Objects[CactuarIndex].Position != CactuarLoadPosition)
        {
            throw new InvalidDataException("Garden allocation lost its authenticated C76630 Cactuar load pose.");
        }
        NativeSavedWorldPose? expectedCarPose = NativeCarWorldRuntimeState.IsSupportedEntity(ControlledEntityType) ? LoadPose : SavedCarSlot?.Pose;
        if (SavedCarSlot is { } savedCar && Objects[SavedCarIndex] != new NativeGardenWorldObject(savedCar.ShapeId, 0, new(expectedCarPose!.X, expectedCarPose.Height, -expectedCarPose.RawZ), 0, unchecked((short)expectedCarPose.Yaw), 0))
        {
            throw new InvalidDataException("Garden appended car differs from its retained original saved pose4.");
        }
        if (NativeCarWorldRuntimeState.IsSupportedEntity(ControlledEntityType) && (SavedCarSlot is null || NativeTrainWorldObject.ClassifyShape(SavedCarSlot.ShapeId) != ControlledEntityType))
        {
            throw new InvalidDataException("Fresh car Garden allocation lost its actual selected vehicle append.");
        }
    }
}

/// <summary>Current ordinary Garden body and dense objects; parking keeps Garden objects separate from the controlled foot record.</summary>
public sealed record NativeGardenWorldRuntimeState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldPose? CarBodyPose { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldBackgroundRailState? BackgroundRails { get; init; }
    public const string RuntimeRule = "ff8-en-garden-ordinary-557a90-548300-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required int Speed { get; init; }
    [JsonRequired]
    public required short GroundYaw { get; init; }
    [JsonRequired]
    public required int CameraFollowSpeed { get; init; }
    [JsonRequired]
    public required short CameraPitch { get; init; }
    [JsonRequired]
    public required byte AnimationVariant { get; init; }
    public int ManualCameraVelocity { get; init; }
    [JsonRequired]
    public required byte MapDisplayMode { get; init; }
    [JsonRequired]
    public required NativeGardenWorldAllocation Allocation { get; init; }
    [JsonRequired]
    public required ImmutableArray<NativeGardenWorldObject> Objects { get; init; }
    [JsonRequired]
    public required NativeGardenCameraDefaults CameraDefaults { get; init; }
    [JsonRequired]
    public required NativeWorldGroundCacheState GroundCache { get; init; }
    [JsonRequired]
    public required int MovementMagnitude20409F4 { get; init; }
    [JsonRequired]
    public required int PrimaryContact { get; init; }
    [JsonRequired]
    public required int BroadContact { get; init; }
    [JsonRequired]
    public required NativeGardenButtonHistory Buttons { get; init; }
    [JsonRequired]
    public required NativeGardenTransferControlState TransferControls { get; init; }
    [JsonRequired]
    public required NativeWorldForestState Forest { get; init; }
    [JsonRequired]
    public NativeCactuarWorldState? Cactuar { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldAuxiliaryRelocationReceipt? AuxiliaryEntry { get; init; }
    [JsonRequired]
    public short? CurrentGroundHeight { get; init; }
    [JsonRequired]
    public bool? PriorTerrainOcclusion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldFootCameraState? ProjectionCamera { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeGardenReboardCamera? ReboardCamera { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeGardenTransferState? Transfer { get; init; }

    public byte EntityType => 48;

    public static NativeGardenWorldRuntimeState CreateFresh(NativeSavedWorldState saved, NativeGardenWorldAllocation allocation, NativeWorldGroundCacheState cache, NativeWorldProcess process, NativeGardenCameraDefaults cameraDefaults, NativeWorldFootCameraState? projectionCamera = null, short? currentGroundHeight = null, bool? priorTerrainOcclusion = null)
    {
        saved.Validate();
        allocation.Validate();
        cache.Validate();
        process.Validate();
        cameraDefaults.Validate();
        NativeSavedWorldPose pose = allocation.LoadPose;
        var result = new NativeGardenWorldRuntimeState
        {
            Version = 1,
            Rule = RuntimeRule,
            Speed = 0,
            GroundYaw = 0,
            CameraFollowSpeed = process.CarCameraFollowSpeed,
            CameraPitch = cameraDefaults.Pitch,
            AnimationVariant = allocation.AnimationVariant,
            ManualCameraVelocity = process.VehicleManualCameraVelocity,
            MapDisplayMode = allocation.MapDisplayMode,
            Allocation = allocation,
            Objects = allocation.Objects,
            CameraDefaults = cameraDefaults,
            GroundCache = cache,
            MovementMagnitude20409F4 = 0,
            // 542B90 initializes both retained C75D0C/C75D10 to-1.
            PrimaryContact = -1,
            BroadContact = -1,
            Buttons = NativeGardenButtonHistory.UnknownAtLoad(),
            TransferControls = NativeGardenTransferControlState.CreateAfterOrdinaryLoad(allocation),
            Forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown),
            Cactuar = allocation.CactuarIndex >= 0 ? NativeCactuarWorldState.CreateAfterLoad() : null,
            CurrentGroundHeight = currentGroundHeight ?? throw new NotSupportedException("Fresh Garden runtime requires the actual53F310/53EB80 selected ground-height output."),
            PriorTerrainOcclusion = priorTerrainOcclusion,
            ProjectionCamera = projectionCamera ?? new NativeWorldFootCameraState(pose.X, pose.RawZ, pose.Height, pose.X, pose.RawZ, pose.Height - 256, allocation.CameraYaw, cameraDefaults.Pitch, cameraDefaults.OffsetY, cameraDefaults.OffsetZ)
        };
        result.Validate();
        return result;
    }

    public void Validate(NativeWorldProcess? process = null)
    {
        if (ManualCameraVelocity is < -128 or > 128 || Version != 1 || Rule != RuntimeRule || Speed is < -64 or > 64 || CameraFollowSpeed is < 0 or > 128 || CameraPitch is < -512 or > 0 || AnimationVariant > 1 || MapDisplayMode > 3 || Allocation is null || Objects.IsDefault || Objects.Length != Allocation.Objects.Length || Objects.Where((item, index) => item is null || item.ShapeId != Allocation.Objects[index].ShapeId || item.Flags != Allocation.Objects[index].Flags || item.Position.X is < -131072 or >= 131072 || item.Position.Z is < -98304 or >= 98304).Any() || CameraDefaults is null || GroundCache is null || Buttons is null || TransferControls is null || Forest is null || Forest.CameraPitch != CameraPitch || CurrentGroundHeight is null || MovementMagnitude20409F4 is < 0 or > 128)
        {
            throw new InvalidDataException("Native Garden runtime lost its current motor, allocation, camera or ground-query owner.");
        }
        Allocation.Validate();
        BackgroundRails?.Validate();
        if (BackgroundRails is { } background && (background.LoadFlags265 & 1) != (Allocation.Flags265 & 1))
        {
            throw new InvalidDataException("Garden background rails differ from the original loader's actual rail-allocation flag.");
        }
        NativeGardenWorldObject? expectedCar = Allocation.SavedCarIndex < 0 ? null : Allocation.Objects[Allocation.SavedCarIndex];
        if (CarBodyPose is { } carPose)
        {
            if (expectedCar is null || carPose.X is < -131072 or >= 131072 || carPose.RawZ is < -98304 or >= 98304 || carPose.Yaw >= 4096)
            {
                throw new InvalidDataException("Garden car-body publication requires its original selected car and native pose widths.");
            }
            expectedCar = expectedCar with { Position = new(carPose.X, carPose.Height, -carPose.RawZ), RotationX = 0, RotationY = unchecked((short)carPose.Yaw), RotationZ = 0 };
        }
        if (expectedCar is not null && Objects[Allocation.SavedCarIndex] != expectedCar)
        {
            throw new InvalidDataException("Garden callbacks cannot move or replace the separately parked saved car.");
        }
        int savedCarIndex = Allocation.SavedCarIndex;
        for (int index = 0; index < Objects.Length; index++)
        {
            byte shape = Objects[index].ShapeId;
            if ((shape == 1 || shape >= 64 && shape is not (64 or 65 or 94) && index != savedCarIndex) && Objects[index] != Allocation.Objects[index])
            {
                throw new InvalidDataException("Garden callbacks cannot move an independently parked ship or a clause coordinate object.");
            }
        }
        AuxiliaryEntry?.RequireGardenPublication(Allocation.Packet, Allocation.LoadPose);
        if ((Allocation.CactuarIndex >= 0) != (Cactuar is not null))
        {
            throw new InvalidDataException("Garden allocation lost its admitted Cactuar process owner.");
        }
        Cactuar?.Validate();
        int maximumContact = Objects.Length - 1 + (BackgroundRails?.Primary is not null || BackgroundRails?.Alternate is not null ? 1 : 0);
        if (PrimaryContact < -1 || PrimaryContact > maximumContact || BroadContact < -1 || BroadContact > maximumContact)
        {
            throw new NotSupportedException("Garden retained contacts require an actual dense allocation index or the native no-contact sentinel.");
        }
        CameraDefaults.Validate();
        GroundCache.Validate();
        Buttons.Validate();
        TransferControls.Validate();
        if (TransferControls.TransitionPhase > 0 && (ReboardCamera is null || Transfer is not null) ||
            TransferControls.TransitionPhase == 0 && ReboardCamera is not null && Transfer is null ||
            ReboardCamera is { ProjectionDistance: < 640 or > 1024 } or { OffsetZ: >= 0 })
        {
            throw new InvalidDataException("Garden reboarding lost its live camera words or phase.");
        }
        Forest.Validate();
        if (process is not null && (CameraFollowSpeed != process.CarCameraFollowSpeed || ManualCameraVelocity != process.VehicleManualCameraVelocity || Forest.CameraCooldown != process.CameraCooldown))
        {
            throw new InvalidDataException("Garden retained camera words differ from their actual shared process owner.");
        }
        ProjectionCamera?.Validate();
        Transfer?.Validate();
    }

    public NativeGardenWorldRuntimeState StepReboardingCamera()
    {
        NativeGardenReboardCamera camera = ReboardCamera ?? throw new InvalidOperationException("Garden reboarding requires the outgoing camera words.");
        // 54B460 has already selected entity 48. 558D70 expands the negative
        // marker to 183 and uses the Garden targets without foot collision XOR.
        int phase = TransferControls.TransitionPhase;
        NativeFootCameraTransition transition = new(phase == int.MinValue ? 183 : phase, camera.ProjectionDistance);
        var next = transition.StepWords(CameraPitch, camera.OffsetY, camera.OffsetZ, 1024, CameraDefaults.Pitch, CameraDefaults.OffsetY, CameraDefaults.OffsetZ, false);
        return this with
        {
            TransferControls = TransferControls with { TransitionPhase = next.Transition.Flags },
            ReboardCamera = next.Transition.Flags == 0 ? null : new(next.Transition.ProjectionDistance, next.OffsetY, next.OffsetZ),
            CameraPitch = next.Pitch,
            Forest = Forest with { CameraPitch = next.Pitch }
        };
    }

    public NativeGardenWorldRuntimeState PublishBody(NativeWorldPathPosition position, short pitch, short yaw, short roll, bool gardenControlled = true)
    {
        Validate();
        ImmutableArray<NativeGardenWorldObject> objects = Objects.SetItem(Allocation.FootObjectIndex, Objects[Allocation.FootObjectIndex] with { Position = position, RotationX = pitch, RotationY = yaw, RotationZ = roll });
        if (gardenControlled)
        {
            objects = objects.SetItem(Allocation.Garden64Index, objects[Allocation.Garden64Index] with { Position = position, RotationX = pitch, RotationY = yaw, RotationZ = roll });
            objects = objects.SetItem(Allocation.Garden65Index, objects[Allocation.Garden65Index] with { Position = position, RotationX = pitch, RotationY = yaw, RotationZ = roll });
        }
        return this with { Objects = objects };
    }

    public NativeGardenWorldRuntimeState PublishCarBody(NativeWorldPathPosition position, short yaw)
    {
        Validate();
        if (Allocation.SavedCarIndex < 0)
        {
            throw new InvalidDataException("Native car publication requires the actual appended Garden-allocation car.");
        }
        NativeGardenWorldObject body = Objects[Allocation.SavedCarIndex] with { Position = position, RotationX = 0, RotationY = yaw, RotationZ = 0 };
        return this with { CarBodyPose = new(position.X, -position.Z, checked((short)position.Y), checked((ushort)yaw)), Objects = Objects.SetItem(Allocation.SavedCarIndex, body).SetItem(Allocation.FootObjectIndex, Objects[Allocation.FootObjectIndex] with { Position = position, RotationX = 0, RotationY = yaw, RotationZ = 0 }) };
    }
}
