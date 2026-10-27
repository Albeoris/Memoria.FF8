using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSonic139ActContext40Tests
{
    private const int MorphOffset = 0xF3C;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleActContext40Policy Policy => NativeBattleActContext40Policy.Sonic139;

    [Theory]
    [InlineData(0x200u)]
    [InlineData(0x300u)]
    [InlineData(0x40200u)]
    [InlineData(0x40300u)]
    public void Alternate_Euler_is_selected_but_Header100_does_not_change_native_row_scaling(uint header)
    {
        NativeBattleDefaultActContext40Step step = Draw(Policy, Source(4, 1), RotatedScaled(header));
        NativeBattleActionMatrix expected = (header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(new(300, 500, 700)) : NativeBattleGfMatrices.Euler701310(new(300, 500, 700));
        expected = NativeBattlePoseMatrices.Scale56BEF0(expected, 2048, 1024, 4096);
        AssertRotation(expected, step);
        Assert.Equal(new int?[] { 0, 0, 2000 }, step.Memory.GeometryRegisters!.Translation);
        Assert.Equal(unchecked((uint)-128), U32(step.Temporary88, 24));
        Assert.Equal(0x2030u, U32(step.Temporary88, 28));
        Assert.Throws<NotSupportedException>(() => step.Temporary88.Read(8, 4));
    }

    [Fact]
    public void Actual_48_vertex_morph_preserves_W_and_remaining_201_record_scratch_after_JSON()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = Memory().Arena!.Write(0x1584, [1, 2, 3, 4]).Write(0x1EF0, [5, 6, 7, 8]) };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        NativeBattleDefaultActContext40Step step = Draw(Policy, Source(48, 2), Node(ratio: 2048, second: 1), memory: restored);
        Assert.Equal((short)98, BinaryPrimitives.ReadInt16LittleEndian(step.Memory.Arena!.Read(MorphOffset + 47 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => step.Memory.Arena.Read(MorphOffset + 47 * 8 + 6, 2));
        Assert.Throws<NotSupportedException>(() => step.Memory.Arena.Read(MorphOffset + 384, 1));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, step.Memory.Arena.Read(0x1584, 4));
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, step.Memory.Arena.Read(0x1EF0, 4));
        Assert.Equal(MorphAddress, U32(step.Temporary88, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(Policy, Source(48, 2), Node(ratio: 2048, second: 1), memory: memory)), NativeBattleTestSnapshots.Json(step));
        Assert.Throws<InvalidDataException>(() => Draw(Policy, Source(48, 2), Node(ratio: 2048, second: 1), Context().Write(36, BitConverter.GetBytes(MorphAddress + 8))));
    }

    [Theory]
    [InlineData(0u, 100)]
    [InlineData(0x200u, 200)]
    public void Direct_translation_and_camera_rotation_branch_preserve_native_depth(uint header, int expectedX)
    {
        NativeBattleActionMatrix context = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Z = 2000 };
        context = NativeBattlePoseMatrices.Scale56BEF0(context, 0, 4096, 4096);
        NativeBattleDefaultActContext40Step step = Draw(Policy, Source(48, 1), Node(header, x: 100), Context(context));
        Assert.Equal(expectedX, step.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(2000, step.Memory.GeometryRegisters.Translation[2]);
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

    private static NativeBattleDefaultActContext40Step Draw(NativeBattleActContext40Policy policy, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null) => NativeBattleDefaultActContext40.Draw(memory ?? Memory(), source, node, context ?? Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, MorphAddress, policy);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 0x648), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(NativeBattleActionMatrix? matrix = null)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 40).Write(0, packed.Bytes.AsSpan()).MarkUnknown(18, 2).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(MorphAddress));
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
    private static NativeBattleGfAnimationSource Source(int count, int variants)
    {
        const int geometry = 16;
        int faceStart = geometry + 12 + count * variants * 8;
        byte[] bytes = new byte[faceStart + 32];
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
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)(vertex + variant * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), 1234);
            }
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }
}
