using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath131ActContext12Tests
{
    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(32767, 65534)]
    [InlineData(-32768, -65536)]
    public void Anchor_plus_node_preserves_full_DWORD_sum_and_does_not_execute_GTE_transform(short value, int expected)
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleGfAnimationNode node = Node(point: new(value, value, value), rotation: new(0, 0, 0));
        NativeBattleBadBreath131ActPreparation step = Prepare(node, Context(new(value, value, value)), memory);
        Assert.Equal(expected, step.Matrix.X);
        Assert.Equal(expected, step.Matrix.Y);
        Assert.Equal(expected + 2000, step.Matrix.Z);
        Assert.Same(memory.ScriptedColorRegisters, step.Memory.ScriptedColorRegisters);
        Assert.Null(step.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(step.Memory.GeometryRegisters.Rotation20[19]);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x40000u)]
    [InlineData(0x8000u)]
    [InlineData(0x200u)]
    [InlineData(0x100u)]
    [InlineData(0x48300u)]
    public void Header_bits_do_not_replace_default_Euler_camera_composition_or_row_scaling(uint header)
    {
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Euler701310(new(511, 1222, -333)) with { X = 10, Y = 20, Z = 2000 };
        NativeBattleGfAnimationNode node = Node(header, point: new(100, 200, 300), scale: new(2048, -4096, 8192));
        NativeBattleBadBreath131ActPreparation step = Prepare(node, Context(new(7, 8, 9)), camera: camera);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = 107, Y = 208, Z = 309 };
        expected = NativeBattleGfMatrices.Compose56C2F0(camera, expected);
        expected = NativeBattlePoseMatrices.Scale56BEF0(expected, 2048, -4096, 8192);
        Assert.Equal(expected.Rotation, step.Matrix.Rotation);
        Assert.Equal(expected.X, step.Matrix.X);
        Assert.Equal(expected.Y, step.Matrix.Y);
        Assert.Equal(expected.Z, step.Matrix.Z);
    }

    [Theory]
    [InlineData(0, 0x2000u)]
    [InlineData(4096, 0x20C0u)]
    [InlineData(-1, 0x20C0u)]
    public void Native_partial_temporary_stores_use_exact_flags_and_keep_unwritten_depth(short scalar, uint flags)
    {
        NativeBattleBadBreath131ActPreparation step = Prepare(Node(scalar: scalar), Context(new(0, 0, 0)));
        Assert.Equal(flags, U32(step.Temporary88, 28));
        Assert.Equal(unchecked((uint)(int)scalar), U32(step.Temporary88, 12));
        Assert.Equal(77u, U32(step.Temporary88, 24));
        if (scalar == 0)
        {
            Assert.Throws<NotSupportedException>(() => step.Temporary88.Read(8, 4));
        }
        else
        {
            Assert.Equal(0xFFAABBCCu, U32(step.Temporary88, 8));
        }
    }

    [Fact]
    public void Interpolation_preserves_W_and_aliases_only_the_current_shared_scratch()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 1320).Write(MorphOffset + 6, BitConverter.GetBytes((short)321)).Write(MorphOffset + 32, [1, 2, 3, 4]) };
        NativeBattleBadBreath131ActPreparation step = Prepare(Node(interpolate: true), Context(new(0, 0, 0)), memory);
        Assert.Equal((short)-350, step.ReadVertex(0).X);
        Assert.Equal((short)321, step.ReadW(0));
        Assert.Null(step.ReadW(8));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, step.Memory.Arena!.Read(MorphOffset + 32, 4));
        Assert.Throws<InvalidDataException>(() => Prepare(Node(interpolate: true), Context(new(0, 0, 0)).Write(8, BitConverter.GetBytes(MorphAddress + 8)), memory));
        NativeBattleBadBreath131ActPreparation direct = Prepare(Node(), Context(new(0, 0, 0)).MarkUnknown(8, 4), Memory() with { Arena = null });
        Assert.Equal((short)0x1234, direct.ReadW(0));
        Assert.Throws<InvalidDataException>(() => direct.ReadVertex(32));
    }

    [Fact]
    public void Reached_572200_backend_uses_native_culling_and_resets_retained_depth()
    {
        NativeBattleBadBreath131ActContextStep step = NativeBattleBadBreath131ActContext12.Draw(Memory(), Source(), Node(rotation: new(0, 0, 0)), Context(new(0, 0, 0)), Camera(), Temporary(), new(null, 100, 160, 108), Colors, MorphAddress, 1320);
        Assert.NotNull(Assert.Single(step.Batch.Attempts).Primitive);
        Assert.Equal(0u, U32(step.Temporary88, 24));
        Assert.Equal(0x2000u, U32(step.Temporary88, 28));
        Assert.NotNull(step.Memory.GeometryRegisters!.Mac0);
    }

    private static NativeBattleBadBreath131ActPreparation Prepare(NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattleEffectMemory? memory = null, NativeBattleActionMatrix? camera = null) => NativeBattleBadBreath131ActContext12.Prepare(memory ?? Memory(), Source(), node, context, Camera(camera), Temporary(), Colors, MorphAddress, 1320);
    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
    }
    private static NativeBattlePackedMatrix32 Camera(NativeBattleActionMatrix? matrix = null) => NativeBattlePackedMatrix32.FromMatrix(matrix ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
    private static NativeBattleEffectArena Context(NativeBattleVector anchor) => NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, BitConverter.GetBytes(anchor.X)).Write(2, BitConverter.GetBytes(anchor.Y)).Write(4, BitConverter.GetBytes(anchor.Z)).Write(8, BitConverter.GetBytes(MorphAddress));
    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(24, BitConverter.GetBytes(77));
    private static NativeBattleGfAnimationNode Node(uint header = 0, NativeBattleVector? point = null, NativeBattleVector? rotation = null, NativeBattleVector? scale = null, short scalar = 0, bool interpolate = false)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Vector(bytes, 8, point ?? new(0, 0, 0));
        Vector(bytes, 16, rotation ?? new(111, 222, 333));
        Vector(bytes, 24, scale ?? new(4096, 4096, 4096));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0xFFAABBCC);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), scalar);
        if (interpolate)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[16 + 12 + 64 + 32 + 36];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 76);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        NativeBattleVector[] positions = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int index = 0; index < 4; index++)
            {
                int at = 28 + (shape * 4 + index) * 8;
                Vector(bytes, at, positions[index] with { X = (short)(positions[index].X + shape * 100) });
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int face = 92 + 28;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 4), 0x3C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x1000000, bytes.ToImmutableArray());
    }
    private static void Vector(byte[] bytes, int offset, NativeBattleVector value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), value.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), value.Z);
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
