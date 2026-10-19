using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelEchoProjectionTests
{
    [Fact]
    public void HistoryCompositionSaturatesEachRotationColumnButRetainsFullMacTranslationFromLowWords()
    {
        NativeBattleActionMatrix basis = new([32767, 0, 0, 0, 32767, 0, 0, 0, 32767], 40000, -40000, 70000, 4096);
        NativeBattleActionMatrix bone = new([32767, 0, 0, 0, 32767, 0, 0, 0, 32767], 65535, 65536, -65535, 4096);
        NativeBattleActionMatrix result = NativeBattleDuelEchoProjection.ComposeHistory460820(basis, bone);
        Assert.Equal(new short[] { 32767, 0, 0, 0, 32767, 0, 0, 0, 32767 }, result.Rotation);
        Assert.Equal(39992, result.X);
        Assert.Equal(-40000, result.Y);
        Assert.Equal(70008, result.Z);
    }

    [Fact]
    public void ProjectionWritesCanonicalRowsAndOnlyClipWordWhenDepthIsZero()
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleModulePoolTask context = new NativeBattleModulePoolTask(0, 0, null, 0, ImmutableArray.CreateRange(new byte[100])).WriteWord(24, 0).WriteWord(26, 0).WriteWord(28, 320).WriteWord(30, 216);
        NativeBattleVector[] points = [new(0, 0, 100), new(320, 216, 100), new(-1, -1, 100), new(0, 0, -100)];
        byte[] source = new byte[2 + points.Length * 6];
        BinaryPrimitives.WriteUInt16LittleEndian(source, checked((ushort)points.Length));
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(2 + index * 6), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(4 + index * 6), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(6 + index * 6), points[index].Z);
        }
        byte[] cache = Enumerable.Repeat((byte)0xCC, 64).ToArray();
        List<(int Offset, int Length)> writes = [];
        NativeBattleDuelEchoProjectionResult result = NativeBattleDuelEchoProjection.Project6A2A90(context, 12, source.ToImmutableArray(), identity, new(identity, 100, 0, 0), 24, (offset, bytes) =>
        {
            writes.Add((offset, bytes.Length));
            bytes.AsSpan().CopyTo(cache.AsSpan(offset));
        });
        Assert.Equal(new[] { (24, 8), (32, 8), (40, 8), (54, 2) }, writes);
        Assert.Equal((ushort)25, BinaryPrimitives.ReadUInt16LittleEndian(cache.AsSpan(28)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(cache.AsSpan(30)));
        Assert.Equal((ushort)10, BinaryPrimitives.ReadUInt16LittleEndian(cache.AsSpan(38)));
        Assert.Equal((ushort)5, BinaryPrimitives.ReadUInt16LittleEndian(cache.AsSpan(46)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(cache.AsSpan(54)));
        Assert.All(cache.AsSpan(48, 6).ToArray(), value => Assert.Equal(0xCC, value));
        Assert.Equal(source.Length, result.SourceBytes);
        Assert.Equal(32, result.VertexBytes);
        Assert.Equal(0u, result.Context.ReadDword(44));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PositiveSz3BelowFourStillUsesOnlyZeroQuarterDepthClipWrite(short z)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleModulePoolTask context = new(0, 0, null, 0, ImmutableArray.CreateRange(new byte[100]));
        byte[] source = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(source, 1);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(6), z);
        List<(int Offset, ImmutableArray<byte> Bytes)> writes = [];
        NativeBattleDuelEchoProjectionResult result = NativeBattleDuelEchoProjection.Project6A2A90(context, 12, source.ToImmutableArray(), identity, new(identity, 100, 0, 0), 0, (offset, bytes) => writes.Add((offset, bytes)));
        Assert.Equal(6, Assert.Single(writes).Offset);
        Assert.Equal(new byte[] { 16, 0 }, writes[0].Bytes);
        Assert.Equal(0u, result.Context.ReadDword(44));
    }

}
