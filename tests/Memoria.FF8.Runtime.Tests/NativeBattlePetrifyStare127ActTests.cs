using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePetrifyStare127ActTests
{
    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Direct_126_vertex_variant_does_not_require_or_touch_the_125_vertex_scratch()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = null };
        NativeBattleEffectArena context = Context().MarkUnknown(12, 4);
        NativeBattlePetrifyStare127ActStep result = Draw(Source(126, 1), Node(), context, memory);
        Assert.Null(result.Memory.Arena);
        Assert.Equal(Source(126, 1).Address + 28, U32(result.Temporary88, 4));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0x2030u, U32(result.Temporary88, 0x1C));
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(8, 4));
        Assert.Null(result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
    }

    [Fact]
    public void Interpolation_writes_125_XYZ_rows_only_and_retains_W_and_the_packet_boundary()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 1000).Write(MorphOffset + 1000, [1, 2, 3, 4]);
        NativeBattlePetrifyStare127ActStep result = Draw(Source(125, 2), Node(ratio: 2048, second: 1), memory: Memory() with { Arena = arena });
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena!.Read(MorphOffset, 2)));
        Assert.Equal((short)174, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena.Read(MorphOffset + 124 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(MorphOffset + 124 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Memory.Arena.Read(MorphOffset + 1000, 4));
        Assert.Equal(MorphAddress, U32(result.Temporary88, 4));
        Assert.Throws<InvalidDataException>(() => Draw(Source(126, 2), Node(ratio: 2048, second: 1)));
        Assert.Throws<InvalidDataException>(() => Draw(Source(125, 2), Node(ratio: 2048, second: 1), Context().Write(12, BitConverter.GetBytes(MorphAddress + 8))));
    }

    [Theory]
    [InlineData(0, 209995, 255, 127)]
    [InlineData(0x200, 139998, 0, 16)]
    public void Header200_uses_rotated_anchor_plus_camera_space_offset_and_retains_MAC32(uint header, int expectedX, byte r0, byte r1)
    {
        NativeBattleActionMatrix view = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleEffectArena context = Context().Write(0, BitConverter.GetBytes((short)10000));
        NativeBattlePetrifyStare127ActStep result = Draw(Source(4, 1), Node(header: header, x: 10000), context, camera: view);
        Assert.Equal(expectedX, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(-3, result.Memory.GeometryRegisters.Translation[1]);
        Assert.Equal((byte?)r0, result.Memory.GeometryRegisters.Rotation20[0]);
        Assert.Equal((byte?)r1, result.Memory.GeometryRegisters.Rotation20[1]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void Alternate_Euler_and_row_scale_preserve_native_translation_and_depth_DWORD()
    {
        NativeBattleGfAnimationNode node = Node(header: 0x40000);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 300);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 500);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 700);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 2048);
        NativeBattlePetrifyStare127ActStep result = Draw(Source(4, 1), node with { Data = bytes.ToImmutableArray() }, Context().Write(8, BitConverter.GetBytes(-70000)));
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler7015B0(new(300, 500, 700)), 2048, 4096, 4096);
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), expected.Rotation[index]);
        }
        Assert.Equal(rotation.Select(value => (byte?)value).ToArray(), result.Memory.GeometryRegisters!.Rotation20.Take(18).ToArray());
        Assert.Equal(2000, result.Memory.GeometryRegisters.Translation[2]);
        Assert.Equal(unchecked((uint)-70000), U32(result.Temporary88, 0x18));
    }

    [Fact]
    public void Cue_writes_full_node_color_and_physical_G3_delivery_uses_current_GTE_and_depth_bias()
    {
        NativeBattlePetrifyStare127ActStep result = Draw(Source(4, 1, 4), Node(cue: 2048, color: 0x7FC08040));
        Assert.Equal(0x20F0u, U32(result.Temporary88, 0x1C));
        Assert.Equal(0x7FC08040u, U32(result.Temporary88, 8));
        Assert.Equal(0x400u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.True(result.Batch.AllocationBytes > 0);
        Assert.Equal(-128, unchecked((int)U32(result.Temporary88, 0x18)));
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(0x10, 4));
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(0x34, 4));
    }

    [Fact]
    public void Current_memory_and_partial_temporary_survive_JSON_next()
    {
        NativeBattlePetrifyStare127ActStep first = Draw(Source(4, 2, 4), Node(ratio: 2048, second: 1, cue: 2048, color: 0xC08040));
        NativeBattlePetrifyStare127ActStep restored = JsonSerializer.Deserialize<NativeBattlePetrifyStare127ActStep>(JsonSerializer.Serialize(first))!;
        NativeBattlePetrifyStare127ActStep Next(NativeBattlePetrifyStare127ActStep current) => NativeBattlePetrifyStare127Act.Draw(current.Memory, Source(4, 2, 4), Node(ratio: 2048, second: 1, cue: 2048, color: 0xC08040), Context(), Packed(), current.Temporary88, new(null, 100, 160, 108), Colors, MorphAddress);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
    }

    private static NativeBattlePetrifyStare127ActStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null, NativeBattleActionMatrix? camera = null) => NativeBattlePetrifyStare127Act.Draw(memory ?? Memory(), source, node, context ?? Context(), Packed(camera), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), new(null, 100, 160, 108), Colors, MorphAddress);
    private static NativeBattlePackedMatrix32 Packed(NativeBattleActionMatrix? camera = null) => NativeBattlePackedMatrix32.FromMatrix(camera ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 1000), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }
    private static NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(0, new byte[6]).Write(8, BitConverter.GetBytes(-128)).Write(12, BitConverter.GetBytes(MorphAddress));
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
