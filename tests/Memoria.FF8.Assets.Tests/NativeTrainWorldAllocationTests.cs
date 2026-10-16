using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTrainWorldAllocationTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Theory]
    [InlineData(290, 0)]
    [InlineData(344, 255)]
    public void OriginalSelectedClauseRetainsNineOrderedObjectsAndPackedRoll(ushort story, byte flags103)
    {
        using WorldAssets assets = new(GameDirectory);
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(story, flags103);
        Assert.Same(allocation, assets.ReadTrainWorldAllocation(story, flags103));
        Assert.Equal(3, allocation.ClauseIndex);
        Assert.Equal((ushort)290, allocation.MinimumStory);
        Assert.Equal((ushort)345, allocation.MaximumStory);
        Assert.Equal(story, allocation.Story);
        Assert.Equal(flags103, allocation.EventFlags103);
        Assert.Equal("74114C3E406D058D782C4E1B1CA20BC8EF6BF44BBFEF07AA25B077FAF2F63519", allocation.ProgramHash);
        Assert.Equal("D69BD2D4C91A98151C73AD180E42EEC941AEE73C41597C9CCDD9AC7E93E54728", allocation.CoordinateHash);
        Assert.Equal("A44987D6A796B846518754F3EB5F0EB39AF9819C62C2CCC664B6112EE674691B", allocation.ClauseHash);
        Assert.Equal(new byte[] { 0, 75, 75, 75, 75, 75, 95, 95, 95 }, allocation.Objects.Select(item => item.ShapeId));
        Assert.Equal(new byte[] { 0, 3, 5, 6, 10, 11, 34, 35, 36 }, allocation.Objects.Select(item => item.CoordinateIndex));
        Assert.Equal(new int[] { 0, 32, 32, 32, 32, 32, -1, -1, -1 }, allocation.Objects.Select(item => item.EntityType));
        Assert.Equal(0, allocation.ControlledObjectIndex);
        Assert.Equal(new NativeSavedWorldPose(0, 0, 0, 0), allocation.Objects[0].Pose);
        Assert.Equal(new NativeWorldPathPosition(-44563, -954, 25082), allocation.Objects[1].Position);
        Assert.Equal(new NativeSavedWorldPose(-44563, -25082, -954, 4524), allocation.Objects[1].Pose);
        Assert.Equal(0x00140340, allocation.Objects[2].PackedOrientation);
        Assert.Equal((short)20, allocation.Objects[2].Roll);
        Assert.Equal((ushort)832, allocation.Objects[2].Pose.Yaw);
        Assert.Equal(new NativeWorldPathPosition(-40070, -562, 6342), allocation.Objects[8].Position);
        NativeTrainWorldAllocation restored = JsonSerializer.Deserialize<NativeTrainWorldAllocation>(JsonSerializer.Serialize(allocation))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(allocation), JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(33, 0)]
    [InlineData(34, 4)]
    [InlineData(204, 4)]
    [InlineData(205, 0)]
    [InlineData(289, 0)]
    public void ExistingSingleFootClausesAndMode2AdmissionRemainUnchanged(ushort story, byte shape)
    {
        using WorldAssets assets = new(GameDirectory);
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(story, 0);
        Assert.Equal(shape, Assert.Single(allocation.Objects).ShapeId);
        Assert.Equal(assets.ReadScriptedFootRecord(story, 0).Pose, allocation.Objects[0].Pose);
        Assert.Equal(assets.ReadFootAllocation(shape == 4 ? (byte)1 : (byte)0, story).ShapeId, shape);
        Assert.Throws<NotSupportedException>(() => assets.ReadScriptedFootRecord(290, 0));
        Assert.Throws<NotSupportedException>(() => assets.ReadFootAllocation(0, 290));
    }

    [Fact]
    public void ControlledObjectSelectionUsesNativeClassOrderRatherThanAnAssumedSlotZero()
    {
        (byte[] program, byte[] coordinates) = OriginalSections();
        int clause = BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan(12));
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(clause + 18), 0x034B);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(clause + 22), 0);
        NativeTrainWorldAllocation allocation = NativeTrainWorldAllocationReader.Read(program, coordinates, 290, 0);
        Assert.Equal(1, allocation.ControlledObjectIndex);
        Assert.Equal((byte)75, allocation.Objects[0].ShapeId);
        Assert.Equal((byte)0, allocation.Objects[1].ShapeId);
        Assert.Throws<InvalidDataException>(() => (allocation with { ControlledObjectIndex = 0 }).Validate());
    }

    [Fact]
    public void UnknownRecordAndNullCoordinateRefuseWithoutMutatingTheSource()
    {
        (byte[] program, byte[] coordinates) = OriginalSections();
        int clause = BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan(12));
        byte[] unknownRecord = (byte[])program.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(unknownRecord.AsSpan(clause + 16), 0xFF12);
        string before = Convert.ToHexString(SHA256.HashData(unknownRecord));
        Assert.Throws<NotSupportedException>(() => NativeTrainWorldAllocationReader.Read(unknownRecord, coordinates, 290, 0));
        Assert.Equal(before, Convert.ToHexString(SHA256.HashData(unknownRecord)));
        byte[] nullCoordinate = (byte[])program.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(nullCoordinate.AsSpan(clause + 18), 0x8000);
        Assert.Throws<NotSupportedException>(() => NativeTrainWorldAllocationReader.Read(nullCoordinate, coordinates, 290, 0));
        byte[] savedVehicle = (byte[])program.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(savedVehicle.AsSpan(clause + 18), 1);
        Assert.Throws<NotSupportedException>(() => NativeTrainWorldAllocationReader.Read(savedVehicle, coordinates, 290, 0));
    }

    [Fact]
    public void OverlappingPointersUnprovedPredicatesAndTruncatedCoordinatesRefuse()
    {
        (byte[] program, byte[] coordinates) = OriginalSections();
        int clause = BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan(12));
        byte[] overlap = (byte[])program.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(overlap.AsSpan(12), clause - 1);
        Assert.Throws<InvalidDataException>(() => NativeTrainWorldAllocationReader.Read(overlap, coordinates, 290, 0));
        byte[] predicate = (byte[])program.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(predicate.AsSpan(clause + 4), 0xFF27);
        Assert.Throws<NotSupportedException>(() => NativeTrainWorldAllocationReader.Read(predicate, coordinates, 290, 0));
        Assert.Throws<InvalidDataException>(() => NativeTrainWorldAllocationReader.Read(program, coordinates.AsSpan(0, 36 * 16 + 15), 290, 0));
        Assert.NotEqual(3, NativeTrainWorldAllocationReader.Read(program, coordinates, 345, 0).ClauseIndex);
    }

    [Fact]
    public void RetainedOperandCoordinateAndControlledIdentityMustAgree()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(290, 0);
        NativeTrainWorldObject item = allocation.Objects[2];
        Assert.Throws<InvalidDataException>(() => (item with { RawOperand = 0x064B }).Validate());
        Assert.Throws<InvalidDataException>(() => (item with { Pose = item.Pose with { Yaw = 0 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (item with { CoordinateBytes = ImmutableArray<byte>.Empty }).Validate());
        Assert.Throws<InvalidDataException>(() => (allocation with { Objects = allocation.Objects.RemoveAt(8) }).Validate());
    }

    private static (byte[] Program, byte[] Coordinates) OriginalSections()
    {
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "world.fs"), FlArchiveRepresentation.Files);
        IFlArchiveEntry entry = archive.Entries.Single(item => item.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("wmsetus.obj", StringComparison.OrdinalIgnoreCase));
        using Stream stream = entry.OpenForRead();
        byte[] member = new byte[checked((int)entry.Size)];
        stream.ReadExactly(member);
        int program = BinaryPrimitives.ReadInt32LittleEndian(member.AsSpan(9 * 4));
        int coordinates = BinaryPrimitives.ReadInt32LittleEndian(member.AsSpan(10 * 4));
        int end = BinaryPrimitives.ReadInt32LittleEndian(member.AsSpan(11 * 4));
        return (member.AsSpan(program, coordinates - program).ToArray(), member.AsSpan(coordinates, end - coordinates).ToArray());
    }
}
