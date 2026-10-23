using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect303Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect303ChildrenTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect303FragmentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Construction_uses_unadvanced_current_FX_scratch_and_exact_separate_random_owners(bool full)
    {
        var resources = NativeBattleEffect303StateTests.Resources();
        byte[] mesh = resources.Meshes[0].Bytes.ToArray();
        for (int vertex = 0; vertex < 48; vertex++)
        {
            Point((short)(vertex % 3 == 1 ? 300 : 0), -100, (short)(vertex % 3 == 2 ? 300 : 0), 77).Read(0, 8).CopyTo(mesh, 8 + vertex * 8);
        }
        int directory = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh));
        int cursor = directory + 12;
        for (int face = 0; face < 76; face++, cursor += 20)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(cursor + 4), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(cursor + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(cursor + 8), 4);
        }
        resources = resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = mesh.ToImmutableArray() }) };
        var frame = Frame();
        (frame, uint main) = Append(frame, Pools[1].Descriptor, MainCallback);
        frame = WriteBytes(frame, main + 28, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(main + 32)), 4);
        for (int row = 0; row < 128; row++)
        {
            var retained = NativeBattleEffectArena.Clear().Write(38, new byte[] { 0xA5, 0x5A }).Write(74, new byte[] { 0x55, 0xAA });
            if (full)
            {
                retained = retained.Write(6, BitConverter.GetBytes((ushort)1));
            }
            frame = WriteBytes(frame, main + 32 + (uint)(row * 76), retained, 76);
        }
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(FirstPacketOffset, Enumerable.Repeat((byte)0xE9, 2432).ToArray()) } };
        var context = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(MeshAddresses[0])).Write(4, BitConverter.GetBytes(main)).Write(8, BitConverter.GetBytes(2)).Write(26, BitConverter.GetBytes((ushort)0xABCD));
        var originalRandom = frame.Random;
        var result = BuildFragments66C3F0(frame, context, resources, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(ArenaAddress + FirstPacketOffset, result.Frame.State.ReadDword(CursorGlobal));
        var expectedRandom = originalRandom;
        for (int draw = 0; draw < 256; draw++)
        {
            expectedRandom = expectedRandom.Draw(out _);
        }
        Assert.Equal(expectedRandom, result.Frame.Random);
        Assert.Equal(unchecked((short)0xABCD), W(ReadBytes(result.Frame, ArenaAddress + FirstPacketOffset, 8), 6));
        uint matrixAddress = ArenaAddress + FirstPacketOffset + 48 * 8;
        Assert.Equal(new byte[] { 0xE9, 0xE9 }, ReadBytes(result.Frame, matrixAddress + 18, 2).Read(0, 2));
        Assert.Equal(Enumerable.Repeat((byte)0xE9, 12), ReadBytes(result.Frame, matrixAddress + 20, 12).Read(0, 12));
        if (full)
        {
            Assert.Equal(0u, result.Frame.State.ReadDword(SeedGlobal));
            Assert.Equal((ushort)0, Read(result.Frame, main).ReadWord(32 + 14));
        }
        else
        {
            var row = ReadBytes(result.Frame, main + 32, 76);
            Assert.Equal((short)100, W(row, 0));
            Assert.Equal((short)-100, W(row, 2));
            Assert.Equal((short)100, W(row, 4));
            Assert.Equal((short)-13, W(row, 8));
            Assert.Equal(new byte[] { 0xA5, 0x5A }, row.Read(38, 2));
            Assert.Equal(new byte[] { 0x55, 0xAA }, row.Read(74, 2));
            uint seed = 0;
            for (int draw = 0; draw < 78 * 2; draw++)
            {
                seed = (seed * 125 + 14) & 32767;
            }
            Assert.Equal(seed, result.Frame.State.ReadDword(SeedGlobal));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Draw_preserves_noncontiguous_UV_and_retained_tail_through_real_append(bool quad)
    {
        var frame = Frame();
        uint main = ArenaAddress;
        var row = Point(0, 0, 30, (short)(quad ? 2 : 1)).Write(16, BitConverter.GetBytes(quad ? 0x2E808080u : 0x26808080u));
        row = row.Write(40, Point(0, 0, 0, 0).Read(0, 6)).Write(46, Point(100, 0, 0, 0).Read(0, 6)).Write(52, Point(0, 100, 0, 0).Read(0, 6)).Write(58, Point(100, 100, 0, 0).Read(0, 6));
        row = row.Write(64, BitConverter.GetBytes(0x3DD41122u)).Write(68, BitConverter.GetBytes(0x00BB3344u)).Write(72, BitConverter.GetBytes((ushort)0x5566)).Write(74, BitConverter.GetBytes((ushort)0x7788));
        frame = WriteBytes(frame, main + 32, row, 76);
        frame = PublishContext(frame, NativeBattleTemporaryMemory.Address, Point(0, 0, 0, 0x1234), 8);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(FirstPacketOffset, Enumerable.Repeat((byte)0xE9, 40).ToArray()) } };
        int calls = 0;
        var actual = DrawFragments66B120(frame, main, NativeBattleEffect303StateTests.Resources(), Inputs((current, operation) =>
        {
            var delivery = Assert.IsType<NativeBattleEffect303DeliverBatch>(operation);
            var attempt = Assert.Single(delivery.Batch.Attempts);
            var polygon = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.Equal(quad ? 10 : 8, polygon.Words.Length);
            Assert.Equal(0x3DD41122u, polygon.Words[3]);
            Assert.Equal(0x00BB3344u, polygon.Words[5]);
            Assert.Equal(new[] { 2030, 2030, 2030, quad ? 2030 : 0 }, polygon.VertexPayload4);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
            int cursor = checked((int)(current.State.ReadDword(CursorGlobal) - ArenaAddress));
            var resolved = NativeBattlePacketBatchWords.Resolve(delivery.Batch, cursor, NativeBattleEffectArena.Length, offset => current.Memory.Arena!.Read(offset, 1)[0]);
            var written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, NativeBattleLogicalOrdering.Clear(), resolved);
            Assert.Equal(0xE9E95566u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena!.Read(cursor + 28, 4)));
            if (quad)
            {
                Assert.Equal(0xE9E97788u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena.Read(cursor + 36, 4)));
            }
            calls++;
            return current with { Memory = written.Memory, State = current.State.WriteDword(CursorGlobal, ArenaAddress + (uint)written.Cursor) };
        }));
        Assert.Equal(1, calls);
        Assert.Equal(ArenaAddress + FirstPacketOffset + (quad ? 40u : 32u), actual.State.ReadDword(CursorGlobal));
        Assert.Equal((short)0x1234, W(Context(actual, NativeBattleTemporaryMemory.Address, 8), 6));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Lifecycle_decrements_first_and_preserves_row_rotation_padding(int life, bool killed)
    {
        var frame = Frame();
        uint main = ArenaAddress;
        var row = Point(10, -100, 20, 1).Write(8, Point(1, -20, 3, (short)life).Read(0, 8));
        row = row.Write(20, Identity().Bytes.AsSpan().Slice(0, 18)).Write(38, new byte[] { 0xAB, 0xCD }).Write(40, Point(100, 200, 300, 400).Read(0, 8)).Write(48, Point(500, 600, 700, 800).Read(0, 8)).Write(56, Point(900, 1000, 1100, 1200).Read(0, 8));
        frame = WriteBytes(frame, main + 32, row, 76);
        var actual = AdvanceFragments66B3A0(frame, main);
        var after = ReadBytes(actual, main + 32, 76);
        Assert.Equal((short)(life - 1), W(after, 14));
        Assert.Equal((short)(killed ? 0 : 1), W(after, 6));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, after.Read(38, 2));
        Assert.Equal(killed ? (short)10 : (short)11, W(after, 0));
        if (killed)
        {
            Assert.Equal(main + 32, U(ReadBytes(actual, main + 28, 4), 0));
        }
    }
}
