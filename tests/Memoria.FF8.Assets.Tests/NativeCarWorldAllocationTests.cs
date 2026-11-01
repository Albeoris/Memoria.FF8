using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeCarWorldAllocationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(4, 3)]
    [InlineData(7, 7)]
    [InlineData(248, 1)]
    public void AppendedAllocationUsesOnlyTheThreeOriginalAuxiliaryBits(byte flags, int count)
    {
        byte[] coordinates = Coordinates();
        NativeSavedWorldVehicleSlot selected = new(85, new(1234, -2345, -100, 1024));
        var objects = NativeCarWorldAllocationReader.ReadAppended(coordinates, selected, flags, 1, new(0, 0, 0));
        NativeTrainWorldObject[] restored = System.Text.Json.JsonSerializer.Deserialize<NativeTrainWorldObject[]>(System.Text.Json.JsonSerializer.Serialize(objects))!;
        Assert.True(objects.Select(item => item.Flags).SequenceEqual(restored.Select(item => item.Flags)));
        Assert.Equal(count, objects.Length);
        Assert.Equal((byte)85, objects[0].ShapeId);
        Assert.Equal(selected.Pose, objects[0].Pose);
        int index = 1;
        for (int group = 0; group < 3; group++)
        {
            if ((flags & (1 << group)) == 0)
            {
                continue;
            }
            Assert.Equal((byte)79, objects[index].ShapeId);
            Assert.Equal((byte)80, objects[index + 1].ShapeId);
            Assert.Equal((byte)0, objects[index].Flags);
            Assert.Equal((byte)4, objects[index + 1].Flags);
            Assert.Equal((byte)(31 + group), objects[index].CoordinateIndex);
            Assert.Equal((byte)(31 + group), objects[index + 1].CoordinateIndex);
            Assert.Equal(1000 + group, objects[index].Position.X);
            Assert.Equal(0x12340000 + group, objects[index].PackedOrientation);
            Assert.True(objects[index].CoordinateBytes.SequenceEqual(objects[index + 1].CoordinateBytes));
            index += 2;
        }
    }

    [Fact]
    public void OptionalCactuarIsBetweenSelectedCarAndAuxiliaryPairs()
    {
        NativeWorldPathPosition cactuar = new(-12345, -600, 54321);
        var objects = NativeCarWorldAllocationReader.ReadAppended(Coordinates(), new(74, new(0, 0, 0, 0)), 1, 0, cactuar);
        Assert.Equal(new byte[] { 74, 94, 79, 80 }, objects.Select(item => item.ShapeId));
        Assert.Equal(cactuar, objects[1].Position);
        Assert.Equal(0, objects[1].PackedOrientation);
    }

    [Fact]
    public void RequestedAuxiliaryPairRequiresItsActualCoordinateRow()
    {
        Assert.Throws<InvalidDataException>(() => NativeCarWorldAllocationReader.ReadAppended(new byte[31 * 16], new(85, new(0, 0, 0, 0)), 1, 1, default));
    }

    private static byte[] Coordinates()
    {
        byte[] coordinates = new byte[34 * 16];
        for (int group = 0; group < 3; group++)
        {
            Span<byte> row = coordinates.AsSpan((31 + group) * 16, 16);
            BinaryPrimitives.WriteInt32LittleEndian(row, 1000 + group);
            BinaryPrimitives.WriteInt32LittleEndian(row[4..], -500 - group);
            BinaryPrimitives.WriteInt32LittleEndian(row[8..], 2000 + group);
            BinaryPrimitives.WriteInt32LittleEndian(row[12..], 0x12340000 + group);
        }
        return coordinates;
    }
}
