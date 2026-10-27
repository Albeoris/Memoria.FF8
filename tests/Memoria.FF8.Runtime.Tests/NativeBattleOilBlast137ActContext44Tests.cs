using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilBlast137ActContext44Tests
{
    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(0, 4, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 2, false)]
    [InlineData(1, 3, true)]
    [InlineData(1, 4, true)]
    [InlineData(1, 5, false)]
    [InlineData(2, 3, false)]
    public void Backend_selection_and_depth_store_follow_exact_context_mode_and_geometry(int mode, int geometry, bool defaultBackend)
    {
        NativeBattleOilBlast137ActPreparation step = Prepare(Node(geometry), Context(mode));
        Assert.Equal(defaultBackend, step.DefaultBackend);
        Assert.Equal(defaultBackend ? -123 : 77, unchecked((int)U32(step.Temporary88, 24)));
        Assert.Equal(0x2030u, U32(step.Temporary88, 28));
        Assert.Throws<NotSupportedException>(() => step.Temporary88.Read(8, 3));
        Assert.Null(step.Memory.GeometryRegisters!.Rotation20[18]);
    }

    [Theory]
    [InlineData(0u, true)]
    [InlineData(0x200u, true)]
    [InlineData(0x8000u, false)]
    [InlineData(0x8200u, false)]
    public void Translation_always_transforms_but_only_header8000_skips_composition(uint header, bool compose)
    {
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 10, Y = 20, Z = 3000 };
        NativeBattleEffectArena context = Context(0, rotation);
        NativeBattleGfAnimationNode node = Node(0, header);
        NativeBattleOilBlast137ActPreparation step = Prepare(node, context);
        NativeBattleActionMatrix expected = compose ? NativeBattleGfMatrices.Multiply56C090(rotation, NativeBattleGfMatrices.Euler701310(node.Vector16)) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        Assert.Equal(expected.Rotation, step.Matrix.Rotation);
        (int X, int Y, int Z) transformed = NativeBattleGfMatrices.Rotate56C3D0(rotation, node.Vector8);
        Assert.Equal(unchecked(transformed.X + rotation.X), step.Matrix.X);
        Assert.Equal(unchecked(transformed.Y + rotation.Y), step.Matrix.Y);
        Assert.Equal(unchecked(transformed.Z + rotation.Z), step.Matrix.Z);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x40000u)]
    public void Euler_and_row_scaling_follow_reached_native_contract(uint header)
    {
        NativeBattleGfAnimationNode node = Node(0, header, scale: new(2048, -4096, 8192));
        NativeBattleOilBlast137ActPreparation step = Prepare(node, Context(0));
        NativeBattleActionMatrix expected = (header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        expected = NativeBattlePoseMatrices.Scale56BEF0(expected, 2048, -4096, 8192);
        Assert.Equal(expected.Rotation, step.Matrix.Rotation);
    }

    [Fact]
    public void Morph_writes_XYZ_only_and_preserves_W_and_adjacent_bytes_through_JSON()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 712).Write(MorphOffset + 6, BitConverter.GetBytes((short)321)).Write(MorphOffset + 712, [11, 22]) };
        NativeBattleOilBlast137ActPreparation step = Prepare(Node(0, interpolate: true), Context(0), memory);
        Assert.Equal((short)50, step.ReadVertex(0).X);
        Assert.Equal((short)321, step.ReadW(0));
        Assert.Null(step.ReadW(8));
        Assert.Equal(new byte[] { 11, 22 }, step.Memory.Arena!.Read(MorphOffset + 712, 2));
        string json = NativeBattleTestSnapshots.Json(step.Memory.Arena);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(json)!;
        Assert.Equal(step.Memory.Arena.Read(MorphOffset, 6), restored.Read(MorphOffset, 6));
        Assert.Throws<NotSupportedException>(() => restored.Read(MorphOffset + 14, 2));
        Assert.Throws<InvalidDataException>(() => Prepare(Node(0, interpolate: true), Context(0).Write(40, BitConverter.GetBytes(MorphAddress + 8)), memory));
    }

    [Fact]
    public void Generic_backend_keeps_retained_depth_unknown_and_does_not_read_morph_for_direct_shape()
    {
        NativeBattleEffectArena context = Context(0).MarkUnknown(32, 4).MarkUnknown(40, 4);
        NativeBattleOilBlast137ActPreparation step = NativeBattleOilBlast137ActContext44.Prepare(Memory() with { Arena = null }, Source(6), Node(0), context, Temporary().MarkUnknown(24, 4), Colors, MorphAddress, 712);
        Assert.False(step.DefaultBackend);
        Assert.Throws<NotSupportedException>(() => step.Temporary88.Read(24, 4));
        Assert.Equal((short)0x1234, step.ReadW(0));
        Assert.Throws<InvalidDataException>(() => step.ReadVertex(32));
        Assert.Throws<NotSupportedException>(() => Prepare(Node(3), Context(1).MarkUnknown(32, 4)));
    }

    [Theory]
    [InlineData(0, 0, 6)]
    [InlineData(1, 3, 7)]
    public void Reached_572200_GT3_and_7043B0_GT4_backends_publish_memory_controls(int mode, int geometry, int group)
    {
        NativeBattleOilBlast137ActContextStep step = NativeBattleOilBlast137ActContext44.Draw(Memory(), Source(group), Node(geometry), Context(mode), Temporary(), new(null, 100, 160, 108), Colors, MorphAddress, 712);
        Assert.NotNull(Assert.Single(step.Batch.Attempts).Primitive);
        Assert.Equal(mode == 1 && geometry is 3 or 4 ? -123 : 0, unchecked((int)U32(step.Temporary88, 24)));
        Assert.NotNull(step.Memory.GeometryRegisters);
        Assert.NotNull(step.Memory.ScriptedColorRegisters);
        Assert.Null(step.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
    }

    private static NativeBattleOilBlast137ActPreparation Prepare(NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattleEffectMemory? memory = null) => NativeBattleOilBlast137ActContext44.Prepare(memory ?? Memory(), Source(7), node, context, Temporary(), Colors, MorphAddress, 712);
    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
    }
    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(24, BitConverter.GetBytes(77));
    private static NativeBattleEffectArena Context(int mode, NativeBattleActionMatrix? matrix = null)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0, packed.Bytes.AsSpan(0, 18)).Write(20, packed.Bytes.AsSpan(20, 12));
        return context.Write(32, BitConverter.GetBytes(-123)).Write(36, BitConverter.GetBytes(mode)).Write(40, BitConverter.GetBytes(MorphAddress));
    }
    private static NativeBattleGfAnimationNode Node(int geometry, uint header = 0, bool interpolate = false, NativeBattleVector? scale = null)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)geometry);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Vector(bytes, 8, new(20, 30, 40));
        Vector(bytes, 16, new(111, 222, 333));
        Vector(bytes, 24, scale ?? new(4096, 4096, 4096));
        if (interpolate)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleGfAnimationSource Source(int group)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[32 + 12 + 64 + 32 + strides[group]];
        for (int index = 0; index < 6; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + index * 4), 32);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 76);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 2);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int at = 44 + (shape * 4 + vertex) * 8;
                Vector(bytes, at, new((short)(shape * 100 + vertex * 200), (short)(vertex % 2 * 200), 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int face = 108 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 4), group == 6 ? 0x34010203u : 0x3C010203u);
        for (int index = 0; index < (group == 6 ? 3 : 4); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x1000000, bytes.ToImmutableArray());
    }
    private static void Vector(byte[] bytes, int offset, NativeBattleVector vector)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vector.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vector.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vector.Z);
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
