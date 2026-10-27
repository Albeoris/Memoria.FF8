using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWhiteWindActMeshTests
{
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Fact]
    public void Morph_of_120_vertices_preserves_adjacent_saved_binding_and_unknown_unused_tail()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0xAEC, 0x3E0).Write(0xEAC, Enumerable.Range(0, 32).Select(value => (byte)(value + 1)).ToArray());
        NativeBattleWhiteWindActMeshStep result = Draw(Source(120), Node(0x200, 2048), arena);
        Assert.Equal(Enumerable.Range(0, 32).Select(value => (byte)(value + 1)), result.Arena.Read(0xEAC, 32));
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0xAEC + 119 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0xAEC + 119 * 8 + 6, 2));
        NativeBattleWhiteWindActMeshStep unknown = Draw(Source(120), Node(0x200, 2048), NativeBattleEffectArena.Clear().MarkUnknown(0xAEC, 0x3E0));
        Assert.Throws<NotSupportedException>(() => unknown.Arena.Read(0xEAC, 32));
        Assert.Equal(NativeBattleTestSnapshots.Json(result.Batch), NativeBattleTestSnapshots.Json(unknown.Batch));
    }

    [Fact]
    public void Malformed_121_vertex_shape_cannot_write_the_adjacent_saved_binding_row()
    {
        Assert.Throws<InvalidDataException>(() => Draw(Source(121), Node(0x200, 2048), NativeBattleEffectArena.Clear()));
    }

    [Fact]
    public void Selected_shape_pointer_retains_original_12_byte_header_and_current_vertex_owner()
    {
        NativeBattleWhiteWindActMeshStep result = Draw(Source(4), Node(0x200, 4096), NativeBattleEffectArena.Clear());
        Assert.Equal(0x10583FCu + 16u + 12u + 32u, Dword(result.Temporary88, 4));
        Assert.Equal(0x2030u, Dword(result.Temporary88, 0x1C));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
    }

    [Fact]
    public void Billboard_8000_retains_Euler_rotation_while_still_publishing_transformed_DWORD_translation()
    {
        NativeBattleActionMatrix billboard = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleWhiteWindActMeshStep result = Draw(Source(4), Node(0x9000, 0, 10000), NativeBattleEffectArena.Clear(), billboard);
        Assert.Equal(79998, result.Registers.Geometry.Translation[0]);
        Assert.Equal((byte?)0, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)16, result.Registers.Geometry.Rotation20[1]);
    }

    [Fact]
    public void Cold_restored_morph_and_register_owners_produce_the_same_next_draw()
    {
        NativeBattleWhiteWindActMeshStep first = Draw(Source(4), Node(0x200, 2048), NativeBattleEffectArena.Clear().MarkUnknown(0xAEC, 0x3E0));
        NativeBattleWhiteWindActMeshStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWhiteWindActMeshStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleWhiteWindActMeshStep Next(NativeBattleWhiteWindActMeshStep prior) => Draw(Source(4), Node(0x200, 1024), prior.Arena, registers: prior.Registers);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static NativeBattleWhiteWindActMeshStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena arena, NativeBattleActionMatrix? billboard = null, NativeBattleGeometryRegisterStep? registers = null)
    {
        NativeBattleActionMatrix root = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 40).Write(0, NativeBattlePackedMatrix32.FromMatrix(root, null).Bytes.AsSpan());
        context = UInt(context, 0x20, unchecked((uint)-128));
        context = UInt(context, 0x24, 0x20DFAB8 + 0xAEC);
        return NativeBattleWhiteWindActMesh.Draw(source, node, context, NativeBattlePackedMatrix32.FromMatrix(billboard ?? root, 0x1234), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), arena, registers ?? new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), new(null, 100, 160, 108), 0, 0, 0x100, Constants);
    }

    private static NativeBattleGfAnimationNode Node(uint header, short ratio, short x = 0)
    {
        byte[] node = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(node.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(8), x);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        return new(0, 0, node.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count)
    {
        int faces = 12 + count * 16;
        byte[] bytes = new byte[16 + faces + 56];
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
                short x = (short)((vertex < 4 ? coordinates[vertex * 2] : 0) + shape * 100);
                short y = (short)(vertex < 4 ? coordinates[vertex * 2 + 1] : 0);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), x);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int group = 16 + faces + 12;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x2C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x10583FC, bytes.ToImmutableArray());
    }

    private static uint Dword(NativeBattleEffectArena arena, int at) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at, 4));
    private static NativeBattleEffectArena UInt(NativeBattleEffectArena arena, int at, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(at, bytes);
    }
}
