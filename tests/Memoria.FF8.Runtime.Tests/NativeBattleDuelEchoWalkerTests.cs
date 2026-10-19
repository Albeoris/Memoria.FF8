using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelEchoWalkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WalkerUsesSelectedBoneMatricesAndCanonicalCpuRowsWithoutReadingSkippedCoordinates(bool clippedFace)
    {
        byte[] source = new byte[200];
        BinaryPrimitives.WriteInt32LittleEndian(source, 2);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 180);
        Word(16, 2);
        Word(18, 1);
        Word(20, 1);
        Word(28, 0);
        Word(30, 3);
        NativeBattleVector[] points = [new(0, 0, 400), new(100, 0, 400), new(0, 100, 400)];
        for (int index = 0; index < points.Length; index++)
        {
            Word(32 + index * 6, unchecked((ushort)points[index].X));
            Word(34 + index * 6, unchecked((ushort)points[index].Y));
            Word(36 + index * 6, unchecked((ushort)points[index].Z));
        }
        Word(52, 1);
        Word(64, clippedFace ? (ushort)0 : (ushort)1);
        Word(66, 2);
        Word(68, 3);
        NativeBattleModulePoolTask echo = NativeBattleDuelEcho.Initialize6A1230(new(0, 0, null, 0, ImmutableArray.CreateRange(new byte[1160])), 12, [0]);
        echo = echo.WriteByte(13, 1).WriteByte(15, 0);
        int matrix = 12 + 136 + 256;
        echo = echo.WriteWord(matrix, 4096).WriteWord(matrix + 8, 4096).WriteWord(matrix + 16, 4096).WriteDword(matrix + 28, 400).MarkPayloadUnknown(matrix + 18, 2);
        NativeBattleModulePoolTask context = new NativeBattleModulePoolTask(0, 0, null, 0, ImmutableArray.CreateRange(new byte[100])).WriteDword(16, 0x196A000).WriteWord(24, 0).WriteWord(26, 0).WriteWord(28, 320).WriteWord(30, 216).WriteDword(32, 0x808080).WriteDword(36, 1);
        byte[] cache = Enumerable.Repeat((byte)0xAA, 32).ToArray();
        bool[] known = new bool[32];
        List<(int Offset, int Length)> writes = [];
        List<NativeBattleGfGeometryBatch> batches = [];
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        context = NativeBattleDuelEchoWalker.Draw68A990(context, 12, 0x1000000, Read, echo, 12, 0, new(identity, 256, 160, 108), Write, ReadCache, batches.Add, 17, 0, 0x155, 0x100);
        Assert.Equal(new[] { (6, 2), (8, 8), (16, 8), (24, 8) }, writes);
        Assert.All(known.Take(6), value => Assert.False(value));
        Assert.All(known.Skip(6), value => Assert.True(value));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(cache.AsSpan(6)));
        Assert.Equal((ushort)200, BinaryPrimitives.ReadUInt16LittleEndian(cache.AsSpan(12)));
        Assert.Equal(0x1000040u, context.ReadDword(12));
        NativeBattleGfGeometryBatch batch = Assert.Single(batches);
        if (clippedFace)
        {
            Assert.Empty(batch.Attempts);
            Assert.Throws<NotSupportedException>(() => context.ReadDword(40));
            Assert.Equal(16, context.ReadWord(46));
        }
        else
        {
            Assert.Equal(32, batch.AllocationBytes);
            Assert.NotNull(Assert.Single(batch.Attempts).Primitive);
        }

        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(offset), value);
        byte[] Read(int offset, int length)
        {
            if (offset < 28 && offset + length > 22 || offset >= 180)
            {
                throw new InvalidOperationException("Native skipped source bytes must remain unconsumed.");
            }
            return source.AsSpan(offset, length).ToArray();
        }
        void Write(int offset, ImmutableArray<byte> bytes)
        {
            writes.Add((offset, bytes.Length));
            bytes.AsSpan().CopyTo(cache.AsSpan(offset));
            Array.Fill(known, true, offset, bytes.Length);
        }
        (byte[] Data, bool[] Known) ReadCache(int offset) => (cache.AsSpan(offset, 8).ToArray(), known.AsSpan(offset, 8).ToArray());
    }
}
