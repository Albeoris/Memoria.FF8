using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEerieSoundWave130ActTests
{
    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(0, 129998)]
    [InlineData(0x200, 60000)]
    public void Header200_uses_direct_camera_space_node_without_anchor_or_context_R(uint header, int expectedX)
    {
        NativeBattleActionMatrix matrix = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleEffectArena context = Context(matrix);
        if (header != 0)
        {
            context = context.MarkUnknown(0, 20);
        }
        NativeBattleEerieSoundWave130ActStep result = Draw(Source(4, 1), Node(header: header, x: 10000), context);
        Assert.Equal(expectedX, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(-3, result.Memory.GeometryRegisters.Translation[1]);
        Assert.Equal(2000, result.Memory.GeometryRegisters.Translation[2]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        if (header == 0)
        {
            Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        }
    }

    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x40100)]
    public void Header100_right_diagonal_scale_is_distinct_from_row_scale_and_retains_translation(uint header)
    {
        NativeBattleGfAnimationNode node = Node(header: header | 0x200, x: 7);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 300);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 500);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 700);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1024);
        NativeBattleEerieSoundWave130ActStep result = Draw(Source(4, 1), node with { Data = bytes.ToImmutableArray() });
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler7015B0(new(300, 500, 700));
        NativeBattleActionMatrix rows = NativeBattlePoseMatrices.Scale56BEF0(euler, 2048, 1024, 4096);
        NativeBattleActionMatrix columns = NativeBattleGfMatrices.Multiply56C090(euler, new([2048, 0, 0, 0, 1024, 0, 0, 0, 4096], 0, 0, 0, 4096));
        Assert.NotEqual(rows.Rotation.ToArray(), columns.Rotation.ToArray());
        NativeBattleActionMatrix expected = (header & 0x100) == 0 ? rows : columns;
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), expected.Rotation[index]);
        }
        Assert.Equal(rotation.Select(value => (byte?)value).ToArray(), result.Memory.GeometryRegisters!.Rotation20.Take(18).ToArray());
        Assert.Equal(7, result.Memory.GeometryRegisters.Translation[0]);
        Assert.Equal(2000, result.Memory.GeometryRegisters.Translation[2]);
    }

    [Fact]
    public void Interpolation_owns_only_96_XYZ_rows_and_preserves_unknown_W_and_adjacent_bytes()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = Memory().Arena!.Write(MorphOffset + 768, [1, 2, 3, 4]) };
        NativeBattleEerieSoundWave130ActStep result = Draw(Source(96, 2), Node(ratio: 2048, second: 1), memory: memory);
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena!.Read(MorphOffset, 2)));
        Assert.Equal((short)146, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena.Read(MorphOffset + 95 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(MorphOffset + 95 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Memory.Arena.Read(MorphOffset + 768, 4));
        Assert.Equal(MorphAddress, U32(result.Temporary88, 4));
        Assert.Throws<InvalidDataException>(() => Draw(Source(97, 2), Node(ratio: 2048, second: 1)));
        NativeBattleEerieSoundWave130ActStep direct = Draw(Source(96, 1), Node(), Context().MarkUnknown(36, 4), Memory() with { Arena = null });
        Assert.Null(direct.Memory.Arena);
    }

    [Fact]
    public void Full_depth_and_conditional_color_stores_feed_current_physical_mesh()
    {
        NativeBattleEerieSoundWave130ActStep result = Draw(Source(4, 1, 4), Node(cue: 2048, color: 0x7FC08040), Context().Write(32, BitConverter.GetBytes(-70000)));
        Assert.Equal(0x20F0u, U32(result.Temporary88, 0x1C));
        Assert.Equal(0x7FC08040u, U32(result.Temporary88, 8));
        Assert.Equal(unchecked((uint)-70000), U32(result.Temporary88, 0x18));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        NativeBattleEerieSoundWave130ActStep noCue = Draw(Source(4, 1), Node());
        Assert.Throws<NotSupportedException>(() => noCue.Temporary88.Read(8, 4));
        Assert.Equal(0x2030u, U32(noCue.Temporary88, 0x1C));
    }

    [Fact]
    public void JSON_next_preserves_current_morph_GTE_and_temporary_knownness()
    {
        NativeBattleEerieSoundWave130ActStep first = Draw(Source(4, 2, 4), Node(ratio: 2048, second: 1, cue: 2048, color: 0xC08040));
        NativeBattleEerieSoundWave130ActStep restored = JsonSerializer.Deserialize<NativeBattleEerieSoundWave130ActStep>(JsonSerializer.Serialize(first))!;
        NativeBattleEerieSoundWave130ActStep Next(NativeBattleEerieSoundWave130ActStep current) => NativeBattleEerieSoundWave130Act.Draw(current.Memory, Source(4, 2, 4), Node(ratio: 2048, second: 1, cue: 2048, color: 0xC08040), Context(), current.Temporary88, new(null, 100, 160, 108), Colors, MorphAddress);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void Custom_morph_length_guard_remains_lazy_and_rejects_non768_only_when_interpolating()
    {
        NativeBattleEerieSoundWave130ActStep direct = NativeBattleEerieSoundWave130Act.Draw(Memory() with { Arena = null }, Source(96, 1), Node(), Context().MarkUnknown(36, 4), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), new(null, 100, 160, 108), Colors, MorphAddress, 760);
        Assert.Null(direct.Memory.Arena);
        Assert.Throws<InvalidDataException>(() => NativeBattleEerieSoundWave130Act.Draw(Memory(), Source(96, 2), Node(ratio: 2048, second: 1), Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), new(null, 100, 160, 108), Colors, MorphAddress, 760));
    }

    private static NativeBattleEerieSoundWave130ActStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null) => NativeBattleEerieSoundWave130Act.Draw(memory ?? Memory(), source, node, context ?? Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), new(null, 100, 160, 108), Colors, MorphAddress);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 768), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
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
