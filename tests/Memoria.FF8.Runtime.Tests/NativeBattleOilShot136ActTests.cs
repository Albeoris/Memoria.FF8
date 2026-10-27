using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilShot136ActTests
{
    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;

    [Theory]
    [InlineData(0x200u)]
    [InlineData(0x300u)]
    [InlineData(0x40200u)]
    [InlineData(0x40300u)]
    public void Native_header_bits_select_alternate_Euler_and_right_diagonal_without_dropping_context_T(uint header)
    {
        NativeBattleDefaultActContext40Step step = Draw(Source(4, 1), RotatedScaled(header));
        NativeBattleActionMatrix matrix = (header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(new(300, 500, 700)) : NativeBattleGfMatrices.Euler701310(new(300, 500, 700));
        matrix = (header & 0x100) != 0 ? matrix with { Rotation = NativeBattleGfMatrices.Multiply56C090(matrix, new([2048, 0, 0, 0, 1024, 0, 0, 0, 4096], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(matrix, 2048, 1024, 4096);
        AssertRotation(matrix, step);
        Assert.Equal(new int?[] { 0, 0, 2000 }, step.Memory.GeometryRegisters!.Translation);
        Assert.Equal(unchecked((uint)-128), U32(step.Temporary88, 24));
        Assert.Equal(0x2030u, U32(step.Temporary88, 28));
        Assert.Throws<NotSupportedException>(() => step.Temporary88.Read(8, 4));
    }

    [Fact]
    public void Direct_144_vertices_does_not_consume_108_vertex_morph_bank_or_unused_pointer()
    {
        NativeBattleDefaultActContext40Step step = Draw(Source(144, 1), Node(), Context().MarkUnknown(40, 4), Memory() with { Arena = null });
        Assert.Null(step.Memory.Arena);
        Assert.Equal(Source(144, 1).Address + 28u, U32(step.Temporary88, 4));
    }

    [Fact]
    public void Original_48_vertex_interpolation_preserves_W_and_the_shared_bank_tail_after_JSON()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = Memory().Arena!.Write(MorphOffset + 864, [1, 2, 3, 4]) };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        NativeBattleDefaultActContext40Step step = Draw(Source(48, 2), Node(ratio: 2048, second: 1), memory: restored);
        Assert.Equal((short)98, BinaryPrimitives.ReadInt16LittleEndian(step.Memory.Arena!.Read(MorphOffset + 47 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => step.Memory.Arena.Read(MorphOffset + 47 * 8 + 6, 2));
        Assert.Throws<NotSupportedException>(() => step.Memory.Arena.Read(MorphOffset + 384, 1));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, step.Memory.Arena.Read(MorphOffset + 864, 4));
        Assert.Equal(MorphAddress, U32(step.Temporary88, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(Source(48, 2), Node(ratio: 2048, second: 1), memory: memory)), NativeBattleTestSnapshots.Json(step));
        Assert.Throws<InvalidDataException>(() => Draw(Source(48, 2), Node(ratio: 2048, second: 1), Context().Write(40, BitConverter.GetBytes(MorphAddress + 8))));
    }

    [Theory]
    [InlineData(0u, 100)]
    [InlineData(0x200u, 200)]
    public void Header200_selects_direct_node_translation_while_other_nodes_use_actual_context_R(uint header, int expectedX)
    {
        NativeBattleActionMatrix context = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Z = 2000 };
        context = NativeBattlePoseMatrices.Scale56BEF0(context, 0, 4096, 4096);
        NativeBattleDefaultActContext40Step step = Draw(Source(4, 1), Node(header, x: 100), Context(context));
        Assert.Equal(expectedX, step.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(2000, step.Memory.GeometryRegisters.Translation[2]);
    }

    [Theory]
    [InlineData(0u, 0, 100)]
    [InlineData(0x200u, 0, 200)]
    [InlineData(0x1000u, 0, 100)]
    [InlineData(0x1200u, 0, 100)]
    [InlineData(0x1000u, 4096, 200)]
    public void Current_matrix_header_has_priority_and_uses_actual_global_R_without_global_T(uint header, int scale, int expectedX)
    {
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Z = 2000 };
        camera = NativeBattlePoseMatrices.Scale56BEF0(camera, 0, 4096, 4096);
        NativeBattleActionMatrix actual = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleCuragaMatrices.Identity8DD770() with { X = 999 }, scale, scale, scale);
        NativeBattleDefaultActContext40Step step = Draw(Source(4, 1), Node(header, x: 100), Context(camera), currentMatrix: NativeBattlePackedMatrix32.FromMatrix(actual, null));
        Assert.Equal(expectedX, step.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(2000, step.Memory.GeometryRegisters.Translation[2]);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(2u, true)]
    public void Mode_selects_custom_for_geometry_zero_without_reading_default_depth(uint mode, bool custom)
    {
        NativeBattleEffectArena context = Context().Write(36, BitConverter.GetBytes(mode));
        if (custom)
        {
            context = context.MarkUnknown(32, 4);
        }
        NativeBattleDefaultActContext40Step step = Draw(Source(4, 1), Node(), context);
        Assert.Equal(0, step.Batch.AllocationBytes);
        if (custom)
        {
            Assert.Throws<NotSupportedException>(() => step.Temporary88.Read(24, 4));
        }
        else
        {
            Assert.Equal(unchecked((uint)-128), U32(step.Temporary88, 24));
        }
    }

    private static NativeBattleGfAnimationNode RotatedScaled(uint header)
    {
        NativeBattleGfAnimationNode node = Node(header);
        byte[] bytes = node.Data.ToArray();
        short[] angles = [300, 500, 700];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + axis * 2, 2), angles[axis]);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24, 2), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26, 2), 1024);
        return node with { Data = bytes.ToImmutableArray() };
    }

    private static void AssertRotation(NativeBattleActionMatrix expected, NativeBattleDefaultActContext40Step result)
    {
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2, 2), expected.Rotation[index]);
        }
        Assert.Equal(rotation.Select(value => (byte?)value), result.Memory.GeometryRegisters!.Rotation20.Take(18));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
    }

    private static NativeBattleDefaultActContext40Step Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null, NativeBattlePackedMatrix32? currentMatrix = null) => NativeBattleOilShot136Act.Draw(memory ?? Memory(), source, node, context ?? Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, MorphAddress, currentMatrix ?? NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 864), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(NativeBattleActionMatrix? matrix = null)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0, packed.Bytes.AsSpan()).MarkUnknown(18, 2).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(0u)).Write(40, BitConverter.GetBytes(MorphAddress));
    }
    private static uint U32(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleGfAnimationNode Node(uint header = 0, short x = 0, short ratio = 0, short second = 0, short cue = 0, uint color = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        foreach (int offset in new[] { 24, 26, 28 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), color);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), cue);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleGfAnimationSource Source(int count, int variants, int group = -1)
    {
        const int geometry = 16;
        int faceStart = geometry + 12 + count * variants * 8;
        byte[] bytes = new byte[faceStart + 32 + (group == 4 ? 20 : 0)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), geometry);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry), faceStart - geometry);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry + 4), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry + 8), variants);
        for (int variant = 0; variant < variants; variant++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int offset = geometry + 12 + (variant * count + vertex) * 8;
                short x = (short)(vertex + variant * 100);
                short y = 0;
                if (group == 4)
                {
                    x = vertex == 1 ? (short)400 : (short)-400;
                    y = vertex >= 2 ? (short)400 : (short)-400;
                }
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), x);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), 1234);
            }
        }
        if (group == 4)
        {
            int face = faceStart + 20;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x30808080);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x808080);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x808080);
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }
}
