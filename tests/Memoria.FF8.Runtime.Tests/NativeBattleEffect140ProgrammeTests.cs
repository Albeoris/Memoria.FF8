using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect140LowerFixture;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140ProgrammeTests
{
    [Theory]
    [InlineData(0x200, 0, 0)]
    [InlineData(0x1000200, 0, 5)]
    [InlineData(0x2000200, 123, 0)]
    public void Header200_skips_unknown_caller_rotation_and_publishes_native_UV_fields(int header, int u, int v)
    {
        NativeBattleEffect140Frame frame = Frame();
        frame = frame with { Staging = frame.Staging!.Write(NativeBattleTemporaryMemory.Address, new byte[108]) };
        NativeBattleGfAnimationSource source = Source();
        NativeBattleEffect140Resources resources = Resources() with { Programmes = ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(source.Address, source) };
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(20, new byte[8]).Write(28, BitConverter.GetBytes(2000)).Write(0x20, BitConverter.GetBytes((short)-128)).Write(0x22, BitConverter.GetBytes((short)5)).Write(0x24, BitConverter.GetBytes(0x20DFAB8u + 100));
        List<NativeBattleEffect140DrawBatch> draws = [];
        NativeBattleEffect140Frame result = NativeBattleEffect140Programme.DrawNode(frame, source, Node((uint)header, false), caller, resources, Inputs((current, operation) =>
        {
            draws.Add(Assert.IsType<NativeBattleEffect140DrawBatch>(operation));
            return current;
        }));
        NativeBattleEffectArena context = ReadBytes(result, NativeBattleTemporaryMemory.Address, 108);
        Assert.Equal((short)u, S(context, 0x20));
        Assert.Equal((short)v, S(context, 0x22));
        Assert.Equal(0x2030u, U(context, 0x1C));
        Assert.Equal(-128, unchecked((int)U(context, 0x18)));
        Assert.Empty(Assert.Single(draws).Batch.Attempts);
        Assert.Equal(2000, result.Memory.GeometryRegisters!.Translation[2]);
    }

    [Fact]
    public void Interior_shape_blend_writes_only_XYZ_to_current_scratch_and_preserves_unknown_W()
    {
        NativeBattleEffect140Frame frame = Frame();
        frame = frame with { Staging = frame.Staging!.Write(NativeBattleTemporaryMemory.Address, new byte[108]), Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(100, 16).Write(116, new byte[] { 1, 2, 3, 4 }) } };
        NativeBattleGfAnimationSource source = Source();
        NativeBattleEffect140Resources resources = Resources() with { Programmes = ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(source.Address, source) };
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(20, new byte[8]).Write(28, BitConverter.GetBytes(2000)).Write(0x24, BitConverter.GetBytes(0x20DFAB8u + 100));
        NativeBattleEffect140Frame result = NativeBattleEffect140Programme.DrawNode(frame, source, Node(0x200, true), caller, resources, Inputs((current, _) => current));
        NativeBattleEffectArena scratch = ReadBytes(result, 0x20DFAB8 + 100, 20);
        Assert.Equal(150, S(scratch, 0));
        Assert.Equal(-150, S(scratch, 2));
        Assert.Throws<NotSupportedException>(() => scratch.Read(6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, scratch.Read(16, 4));
        Assert.Equal(0x20DFAB8u + 100, U(ReadBytes(result, NativeBattleTemporaryMemory.Address, 108), 4));
    }

    private static NativeBattleGfAnimationNode Node(uint header, bool morph)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        if (morph)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[92];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 44);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 2; vertex++)
            {
                int at = 28 + (shape * 2 + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)(100 + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), (short)(-100 - shape * 100));
            }
        }
        return new(0x11C6C54, bytes.ToImmutableArray());
    }
}
