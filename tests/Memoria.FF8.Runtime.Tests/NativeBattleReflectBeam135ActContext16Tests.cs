using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReflectBeam135ActContext16Tests
{
    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(0u)]
    [InlineData(0x100u)]
    [InlineData(0x40000u)]
    [InlineData(0x40100u)]
    public void Header_selects_original_Euler_and_right_diagonal_or_row_scale(uint header)
    {
        byte[] data = Node().Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        short[] rotation = [511, 733, -999];
        short[] scale = [2048, -4096, 8192];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + axis * 2), rotation[axis]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + axis * 2), scale[axis]);
        }
        NativeBattleGfAnimationNode node = new(0, 0, data.ToImmutableArray());
        NativeBattleAnchorActPreparationStep actual = Prepare(Source(4), node, Context(-128));
        NativeBattleActionMatrix expected = (header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        if ((header & 0x100) != 0)
        {
            expected = NativeBattleGfMatrices.Multiply56C090(expected, new([2048, 0, 0, 0, -4096, 0, 0, 0, 8192], 0, 0, 0, 4096));
        }
        else
        {
            expected = NativeBattlePoseMatrices.Scale56BEF0(expected, 2048, -4096, 8192);
        }
        Assert.Equal(expected.Rotation, actual.Matrix.Rotation);
        Assert.Equal(2000, actual.Matrix.Z);
        Assert.Equal(unchecked((uint)-128), U32(actual.Temporary88, 24));
        Assert.Equal(0x2030u, U32(actual.Temporary88, 28));
        Assert.Throws<NotSupportedException>(() => actual.Temporary88.Read(8, 4));
    }

    [Fact]
    public void Original_68_vertex_morph_writes_XYZ_only_and_keeps_PRIVATE_boundary()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 544).Write(MorphOffset + 544, [1, 2, 3, 4]);
        NativeBattleAnchorActPreparationStep step = Prepare(Source(68), Node(true), Context(-128), arena);
        Assert.Null(step.ReadVertex(67 * 8).W);
        Assert.Equal((short)50, step.ReadVertex(67 * 8).Vertex.X);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, step.Arena.Read(MorphOffset + 544, 4));
        Assert.Throws<InvalidDataException>(() => Prepare(Source(69), Node(true), Context(0)));
        Assert.Throws<InvalidDataException>(() => Prepare(Source(68), Node(true), Context(0).Write(12, BitConverter.GetBytes(MorphAddress + 8))));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x200u)]
    public void Header_anchor_branch_preserves_WORD_wrap_and_actual_GTE_vector_W(uint header)
    {
        byte[] data = Node().Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 32767);
        NativeBattleEffectArena context = Context(-128).Write(0, BitConverter.GetBytes((short)32767)).MarkUnknown(6, 2);
        NativeBattleAnchorActPreparationStep step = Prepare(Source(4), new(0, 0, data.ToImmutableArray()), context);
        Assert.Equal(header == 0 ? -2 : 65534, step.Matrix.X);
        Assert.False(step.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(step.Registers.Colors.NormalInputKnown.Value[7]);
    }

    [Fact]
    public void Dedicated_draw_uses_original_default_7043_backend_and_signed_depth()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
        NativeBattleReflectBeam135ActContext16Step step = NativeBattleReflectBeam135ActContext16.Draw(memory, Source(4), Node(), Context(-128), Camera(), Temporary(), new(null, 100, 160, 108), Constants, MorphAddress, 544);
        Assert.Single(step.Batch.Attempts);
        Assert.Equal(unchecked((uint)-128), U32(step.Temporary88, 24));
        Assert.Null(step.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
    }

    private static NativeBattleAnchorActPreparationStep Prepare(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattleEffectArena? arena = null)
    {
        return NativeBattleAnchorActPreparation.Prepare(source, node, context, Camera(), Temporary(), arena ?? NativeBattleEffectArena.Clear(), MorphAddress, 544, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), Constants, NativeBattleAnchorActContextPolicy.ReflectBeam135);
    }

    private static NativeBattlePackedMatrix32 Camera() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88);
    private static NativeBattleEffectArena Context(int depth) => NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(0, new byte[8]).Write(8, BitConverter.GetBytes(depth)).Write(12, BitConverter.GetBytes(MorphAddress));

    private static NativeBattleGfAnimationNode Node(bool interpolate = false)
    {
        byte[] node = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        if (interpolate)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        }
        return new(0, 0, node.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count)
    {
        int faces = 12 + count * 16;
        byte[] bytes = new byte[16 + faces + 68];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        short[] coordinates = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int at = 28 + (shape * count + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)((vertex < 4 ? coordinates[vertex * 2] : 0) + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), (short)(vertex < 4 ? coordinates[vertex * 2 + 1] : 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int group = 16 + faces + 28;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x3C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
