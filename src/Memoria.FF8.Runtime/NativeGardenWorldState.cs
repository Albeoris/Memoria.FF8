using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeGardenWorldObject([property: JsonRequired] byte ShapeId, [property: JsonRequired] byte Flags, [property: JsonRequired] NativeWorldPathPosition Position, [property: JsonRequired] short RotationX, [property: JsonRequired] short RotationY, [property: JsonRequired] short RotationZ)
{
    public int EntityType => NativeTrainWorldObject.ClassifyShape(ShapeId);
}

public sealed record NativeGardenCameraDefaults([property: JsonRequired] string ExecutableHash, [property: JsonRequired] short Pitch, [property: JsonRequired] short OffsetX, [property: JsonRequired] short OffsetY, [property: JsonRequired] short OffsetZ)
{
    public void Validate()
    {
        if (ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || Pitch != -112 || OffsetX != 0 || OffsetY != -341 || OffsetZ != -7168)
        {
            throw new NotSupportedException("Garden camera words differ from the authenticated557250/558C20 ordinary projection contract.");
        }
    }
}

/// <summary>Executed544860/548080 allocation after the fresh loader's dominating548300 pose publication.</summary>
public sealed record NativeGardenWorldAllocation([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string CoordinateHash, [property: JsonRequired] string ClauseHash, [property: JsonRequired] int ClauseIndex, [property: JsonRequired] ushort MinimumStory, [property: JsonRequired] ushort MaximumStory, [property: JsonRequired] ushort Story, [property: JsonRequired] byte Flags265, [property: JsonRequired] byte ChocoboFlags1536, [property: JsonRequired] ImmutableArray<ushort> ClauseWords, [property: JsonRequired] ImmutableArray<NativeGardenWorldObject> Objects, [property: JsonRequired] int FootObjectIndex, [property: JsonRequired] int Garden64Index, [property: JsonRequired] int Garden65Index, [property: JsonRequired] WorldTransition Packet, [property: JsonRequired] NativeSavedWorldPose LoadPose, [property: JsonRequired] NativeSavedWorldPose GardenPose, [property: JsonRequired] ushort CameraYaw, [property: JsonRequired] byte AnimationVariant, [property: JsonRequired] byte MapDisplayMode)
{
    public const string AllocationRule = "ff8-en-garden-544860-548080-548300-v1";
    [JsonRequired]
    public byte ReplacementFlags264 { get; init; }
    [JsonRequired]
    public byte GuardianForce13Flags { get; init; }
    [JsonRequired]
    public NativeWorldPathPosition CactuarLoadPosition { get; init; }
    [JsonIgnore]
    public int CactuarIndex => Objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId == 94).Select(pair => pair.index).DefaultIfEmpty(-1).Single();

    public void Validate()
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
        if (Version != 1 || Rule != AllocationRule || !Hash(ProgramHash) || !Hash(CoordinateHash) || !Hash(ClauseHash) || ClauseIndex < 0 || MinimumStory >= MaximumStory || Story < MinimumStory || Story >= MaximumStory || ClauseWords.IsDefaultOrEmpty || Objects.IsDefaultOrEmpty || Objects.Length > 64 || Objects.Any(item => item is null) || Packet is null || unchecked((byte)Packet.Mode) is not (0 or 48) || unchecked((sbyte)Packet.Extra) != -1 || LoadPose is null || LoadPose.X is < -131072 or >= 131072 || LoadPose.RawZ is < -98304 or >= 98304 || LoadPose.Yaw >= 4096 || GardenPose is null || GardenPose.X is < -131072 or >= 131072 || GardenPose.RawZ is < -98304 or >= 98304 || GardenPose.Yaw >= 4096 || CameraYaw >= 4096 || AnimationVariant != 0 || MapDisplayMode > 3)
        {
            throw new InvalidDataException("Native Garden allocation lacks its original clause, exact loader packet or current pose authority.");
        }
        if ((uint)FootObjectIndex >= Objects.Length || (uint)Garden64Index >= Objects.Length || (uint)Garden65Index >= Objects.Length || Objects[FootObjectIndex].ShapeId != 0 || Objects[Garden64Index].ShapeId != 64 || Objects[Garden65Index].ShapeId != 65)
        {
            throw new InvalidDataException("Garden allocation lost its distinct foot and shape64/65 native object identities.");
        }
        ushort[] header = [0xFF01, 0, 0xFF02, MinimumStory, 0xFF03, MaximumStory, 0xFF04, 0];
        bool whiteSeeDShip = ClauseWords.Length == 22 && ClauseWords[18] == 0xFF13 && ClauseWords[19] == 0x1958;
        ushort[] body = whiteSeeDShip ? [0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 64, 0xFF13, 65, 0xFF13, 0x1958, 0xFF05, 0] : [0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 64, 0xFF13, 65, 0xFF05, 0];
        if (ClauseWords.Length != header.Length + body.Length || !ClauseWords.AsSpan()[..8].SequenceEqual(header) || !ClauseWords.AsSpan()[8..].SequenceEqual(body))
        {
            throw new NotSupportedException("Garden allocation requires the reached complete ordinary foot/chocobo/Garden clause.");
        }
        List<byte> expected = (ChocoboFlags1536 & 3) == 1 ? [0, 2, 3, 64, 65] : [0, 2, 64, 65];
        int gardenIndex = expected.Count - 2;
        if (whiteSeeDShip)
        {
            expected.Add(88);
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
        if (!Objects.Select(item => item.ShapeId).SequenceEqual(expected) || FootObjectIndex != 0 || Garden64Index != gardenIndex || Garden65Index != gardenIndex + 1 || Objects.Any(item => item.Flags != (item.ShapeId == 65 ? 2 : item.ShapeId == 80 ? 4 : 0)))
        {
            throw new InvalidDataException("Garden dense allocation differs from its exact1536 conditional admission and original object flags.");
        }
        if (CactuarIndex >= 0 && Objects[CactuarIndex].Position != CactuarLoadPosition)
        {
            throw new InvalidDataException("Garden allocation lost its authenticated C76630 Cactuar load pose.");
        }
    }
}

/// <summary>Current ordinary Garden body and dense objects; parking keeps Garden objects separate from the controlled foot record.</summary>
public sealed record NativeGardenWorldRuntimeState
{
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
        if (Version != 1 || Rule != RuntimeRule || Speed is < -64 or > 64 || CameraFollowSpeed is < 0 or > 128 || CameraPitch is < -512 or > 0 || AnimationVariant != 0 || MapDisplayMode > 3 || Allocation is null || Objects.IsDefault || Objects.Length != Allocation.Objects.Length || Objects.Where((item, index) => item is null || item.ShapeId != Allocation.Objects[index].ShapeId || item.Flags != Allocation.Objects[index].Flags || item.Position.X is < -131072 or >= 131072 || item.Position.Z is < -98304 or >= 98304).Any() || CameraDefaults is null || GroundCache is null || Buttons is null || TransferControls is null || Forest is null || Forest.CameraPitch != CameraPitch || CurrentGroundHeight is null || MovementMagnitude20409F4 is < 0 or > 128)
        {
            throw new InvalidDataException("Native Garden runtime lost its current motor, allocation, camera or ground-query owner.");
        }
        Allocation.Validate();
        AuxiliaryEntry?.RequireGardenPublication(Allocation.Packet, Allocation.LoadPose);
        if ((Allocation.CactuarIndex >= 0) != (Cactuar is not null))
        {
            throw new InvalidDataException("Garden allocation lost its admitted Cactuar process owner.");
        }
        Cactuar?.Validate();
        if (PrimaryContact < -1 || PrimaryContact >= Objects.Length || BroadContact < -1 || BroadContact >= Objects.Length)
        {
            throw new NotSupportedException("Garden retained contacts require an actual dense allocation index or the native no-contact sentinel.");
        }
        CameraDefaults.Validate();
        GroundCache.Validate();
        Buttons.Validate();
        TransferControls.Validate();
        Forest.Validate();
        if (process is not null && (CameraFollowSpeed != process.CarCameraFollowSpeed || Forest.CameraCooldown != process.CameraCooldown))
        {
            throw new InvalidDataException("Garden retained camera words differ from their actual shared process owner.");
        }
        ProjectionCamera?.Validate();
        Transfer?.Validate();
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
}
