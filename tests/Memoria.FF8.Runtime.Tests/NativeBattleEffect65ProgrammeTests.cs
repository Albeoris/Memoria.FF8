using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect65LowerFixture;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65ProgrammeTests
{
    [Fact]
    public void Current_source_pointer_resolves_file_extent_after_directory_has_been_overwritten()
    {
        NativeBattleEffect65Frame frame = Frame();
        int offset = BundleProgrammeOffsets[394][6];
        int length = BundleLengths[394] - offset;
        uint address = ArenaAddress + FileOffset + (uint)offset;
        frame = Write(frame, address, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA7, length).ToArray()), length);
        frame = Write(frame, ArenaAddress + FileOffset, NativeBattleEffectArena.Clear().Write(0, new byte[32]), 32);
        NativeBattleGfAnimationSource result = NativeBattleEffect65Programme.ReadSource(frame, address, Resources());
        Assert.Equal(address, result.Address);
        Assert.Equal(length, result.Data.Length);
        Assert.All(result.Data, value => Assert.Equal((byte)0xA7, value));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect65Programme.ReadSource(frame, address + 1, Resources()));
    }

    [Fact]
    public void Current_source_requires_actual_loaded_suffix_knownness()
    {
        NativeBattleEffect65Frame frame = Frame();
        uint address = ArenaAddress + FileOffset + (uint)BundleProgrammeOffsets[394][4];
        int length = BundleLengths[394] - BundleProgrammeOffsets[394][4];
        frame = Write(frame, address, NativeBattleEffectArena.Clear().MarkUnknown(length - 1, 1), length);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect65Programme.ReadSource(frame, address, Resources()));
    }

    [Fact]
    public void Distinct_shape_interpolation_writes_saturated_XYZ_and_preserves_unknown_W_and_next_owner()
    {
        NativeBattleEffect65Frame frame = Frame();
        frame = Write(frame, ArenaAddress + 0x6CB8, NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(16, new byte[] { 1, 2, 3, 4 }), 20);
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = new(null, null, null, 0, null) { ModelRendererRegisters = new(null, null, null, null, null) } } };
        List<NativeBattleEffect65Operation> operations = [];
        frame = NativeBattleEffect65Programme.DrawNode(frame, Source(), Node(), Caller(), Resources(), Inputs(operations), 0x1D8E054);
        NativeBattleEffectArena rows = Read(frame, ArenaAddress + 0x6CB8, 20);
        Assert.Equal((short)150, Word(rows, 0));
        Assert.Equal((short)-150, Word(rows, 2));
        Assert.Equal((short)0, Word(rows, 4));
        Assert.Throws<NotSupportedException>(() => rows.Read(6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, rows.Read(16, 4));
        Assert.Equal(0x1D8E054u, Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(operations)).CursorGlobal);
        Assert.Equal(0x200Cu, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x1C, 4)));
    }

    private static NativeBattleEffectArena Caller() => NativeBattleEffectArena.Clear().Write(0, Identity(2000).Bytes.AsSpan()).Write(0x20, BitConverter.GetBytes(0x1342D2Cu)).Write(0x2C, BitConverter.GetBytes(ArenaAddress + 0x6CB8));

    private static NativeBattleGfAnimationNode Node()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x6200);
        foreach (int offset in new[] { 24, 26, 28 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[92];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 44);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 2; vertex++)
            {
                int offset = 28 + (shape * 2 + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)(100 + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), (short)(-100 - shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), 1234);
            }
        }
        return new(0x100000, bytes.ToImmutableArray());
    }
}
