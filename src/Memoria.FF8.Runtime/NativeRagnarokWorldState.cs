using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeRagnarokWorldObject([property: JsonRequired] byte ShapeId, [property: JsonRequired] byte Flags, [property: JsonRequired] NativeWorldPathPosition Position, [property: JsonRequired] short Pitch, [property: JsonRequired] short Yaw, [property: JsonRequired] short Roll);

/// <summary>Original section9 clause14 and544860/548080/548300 dense-object publication, before ground/contact initialization.</summary>
public sealed record NativeRagnarokWorldAllocation([property: JsonRequired] string ProgramHash, [property: JsonRequired] string CoordinateHash, [property: JsonRequired] string ClauseHash, [property: JsonRequired] ushort Story, [property: JsonRequired] byte ChocoboFlags1536, [property: JsonRequired] byte ReplacementFlags264, [property: JsonRequired] byte GuardianForce13Flags, [property: JsonRequired] ImmutableArray<ushort> ClauseWords, [property: JsonRequired] ImmutableArray<NativeRagnarokWorldObject> Objects, [property: JsonRequired] int RagnarokIndex, [property: JsonRequired] WorldTransition Packet, [property: JsonRequired] NativeSavedWorldPose LoadPose, [property: JsonRequired] ushort CameraYaw, [property: JsonRequired] byte MapDisplayMode)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldFootReturn? BattleReload { get; init; }
    [JsonIgnore]
    public byte ControlledEntityType => BattleReload?.EntityType105 ?? unchecked((byte)Packet.Mode);

    public const string Rule = "ff8-en-ragnarok-section9-clause14-544860-548080-548300-v1";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldPose? ParkedRagnarokPose2 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldAuxiliaryRelocationReceipt? AuxiliaryEntry { get; init; }

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
        static bool Hash(string? value)
        {
            return value is { Length: 64 } && value.All(Uri.IsHexDigit);
        }
        ushort[] expectedClause = [0xFF01, 0, 0xFF02, 3100, 0xFF03, 3900, 0xFF04, 0, 0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 1, 0xFF13, 64, 0xFF13, 65, 0xFF13, 7514, 0xFF13, 7515, 0xFF05, 0];
        if (!Hash(ProgramHash) || !Hash(CoordinateHash) || !Hash(ClauseHash) || Story is < 3100 or >= 3900 || ClauseWords.IsDefault || !ClauseWords.AsSpan().SequenceEqual(expectedClause) || Objects.IsDefaultOrEmpty || Objects.Any(item => item is null) || Packet is null || ControlledEntityType is not (0 or 50) || ControlledEntityType == 0 && ParkedRagnarokPose2 is null || ControlledEntityType == 50 && ParkedRagnarokPose2 is not null || ControlledEntityType == 50 && unchecked((sbyte)Packet.Extra) != -1 || LoadPose is null || LoadPose.X is < -131072 or >= 131072 || LoadPose.RawZ is < -98304 or >= 98304 || LoadPose.Yaw >= 4096 || CameraYaw >= 4096 || MapDisplayMode > 3)
        {
            throw new InvalidDataException("Ragnarok allocation requires the exact original selected clause, loader packet and published pose owners.");
        }
        List<byte> expected = (ChocoboFlags1536 & 3) == 1 ? [0, 2, 3, 1, 64, 65, 90, 91] : [0, 2, 1, 64, 65, 90, 91];
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
        if (!Objects.Select(item => item.ShapeId).SequenceEqual(expected) || RagnarokIndex != expected.IndexOf(1) || Objects.Any(item => item.Flags != (item.ShapeId == 65 ? 2 : item.ShapeId == 80 ? 4 : 0)))
        {
            throw new InvalidDataException("Ragnarok allocation lost the native dense shape1 owner or original conditional objects.");
        }
        AuxiliaryEntry?.RequireRagnarokPublication(Packet, LoadPose);
        foreach (NativeRagnarokWorldObject item in Objects.Where(item => item.ShapeId is 0 or 1 or 3))
        {
            NativeSavedWorldPose pose = item.ShapeId == 1 && ParkedRagnarokPose2 is not null ? ParkedRagnarokPose2 : LoadPose;
            NativeWorldPathPosition expectedPosition = new(pose.X, pose.Height, -pose.RawZ);
            short expectedYaw = unchecked((short)pose.Yaw);
            if (item.ShapeId == 1 && ControlledEntityType == 0 && AuxiliaryEntry?.Relocations.LastOrDefault(row => row.EntityType == 50) is { } parkedRelocation)
            {
                expectedPosition = parkedRelocation.Position;
                expectedYaw = parkedRelocation.Yaw;
            }
            if (pose.X is < -131072 or >= 131072 || pose.RawZ is < -98304 or >= 98304 || pose.Yaw >= 4096 || item.Position != expectedPosition || item.Pitch != 0 || item.Yaw != expectedYaw || item.Roll != 0)
            {
                throw new InvalidDataException("Ragnarok fresh548300 publication differs from its foot, conditional chocobo or shape1 pose owner.");
            }
        }
    }
}

/// <summary>Component checkpoint only; does not stand in for full world ground/contact/transfer persistence.</summary>
public sealed record NativeRagnarokWorldRuntimeState([property: JsonRequired] NativeRagnarokWorldAllocation Allocation, [property: JsonRequired] ImmutableArray<NativeRagnarokWorldObject> Objects, [property: JsonRequired] NativeRagnarokMotorState Motor)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldGroundCacheState? GroundCache { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldFootCameraState? ProjectionCamera { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? PreviousButtons { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? CurrentGroundHeight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeRagnarokTransferState? Transfer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldPathPosition? LandingPlacement { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PrimaryContact { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BroadContact { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldForestState? Forest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? GroundYaw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PriorTerrainOcclusion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeCactuarWorldState? Cactuar { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeRagnarokTransferCamera? TransferCamera { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProjectionCurveThreshold { get; init; }

    public void RequireSceneState()
    {
        Validate();
        if (GroundCache is null || ProjectionCamera is null || CurrentGroundHeight is null || PrimaryContact is null || BroadContact is null)
        {
            throw new NotSupportedException("Ragnarok scene requires its actual ground/cache, pre-terrain projection camera and native current ground height.");
        }
        GroundCache.Validate();
        ProjectionCamera.Validate();
        Transfer?.Validate();
        Forest?.Validate();
        Cactuar?.Validate();
    }

    public static NativeRagnarokWorldRuntimeState CreateAfterLoader(NativeRagnarokWorldAllocation allocation, short cameraFollowSpeed204DAE4, short verticalBias204DADC)
    {
        return CreateAfterLoader(allocation, cameraFollowSpeed204DAE4, NativeWorldVerticalBiasState.Exact(verticalBias204DADC));
    }

    public static NativeRagnarokWorldRuntimeState CreateAfterLoader(NativeRagnarokWorldAllocation allocation, short cameraFollowSpeed204DAE4, NativeWorldVerticalBiasState verticalBias)
    {
        allocation.Validate();
        verticalBias.Validate();
        //557250 clears body pitch/roll;53DC70 copies the signed height WORD.
        short height = allocation.LoadPose.Height;
        var motor = new NativeRagnarokMotorState(0, allocation.LoadPose.Yaw, 0, 0, height, allocation.CameraYaw, unchecked((short)(-256 - height / 24)), cameraFollowSpeed204DAE4, verticalBias.IsExact ? verticalBias.Minimum : null) { VerticalBiasAuthority = verticalBias.IsExact ? null : verticalBias };
        var result = new NativeRagnarokWorldRuntimeState(allocation, allocation.Objects, motor);
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Allocation is null || Objects.IsDefaultOrEmpty || Motor is null)
        {
            throw new InvalidDataException("Ragnarok runtime requires explicit allocation, dense objects and retained motor owners.");
        }
        Allocation.Validate();
        Motor.Validate();
        if (Objects.Length != Allocation.Objects.Length || Objects.Where((item, index) => item is null || item.ShapeId != Allocation.Objects[index].ShapeId || item.Flags != Allocation.Objects[index].Flags || item.Position.X is < -131072 or >= 131072 || item.Position.Z is < -98304 or >= 98304).Any())
        {
            throw new InvalidDataException("Ragnarok runtime lost an allocated native object identity or centered pose.");
        }
    }

    public NativeRagnarokWorldRuntimeState PublishBody(NativeWorldPathPosition position, short pitch, short yaw, short roll)
    {
        Validate();
        var objects = Objects.ToBuilder();
        for (int index = 0; index < objects.Count; index++)
        {
            //548300 updates C76640, conditional C76648 and C7664C for controlled50.
            if (objects[index].ShapeId is 0 or 1 or 3)
            {
                objects[index] = objects[index] with { Position = position, Pitch = pitch, Yaw = yaw, Roll = roll };
            }
        }
        var result = this with { Objects = objects.ToImmutable() };
        result.Validate();
        return result;
    }
}
