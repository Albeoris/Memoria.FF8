using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325WingMorphTests
{
    private const uint Address = 0x20DFAB8;

    [Fact]
    public void Vertex_delta_narrows_before_rounding_and_addition_and_preserves_unsigned_start_and_padding()
    {
        byte[] first = Vertex(-30000, 1, 255);
        byte[] second = Vertex(30000, 1, 255);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1000, first).Write(0x2000, second).Write(0x3000, Enumerable.Repeat((byte)0xCC, 8).ToArray()).MarkUnknown(0x100E, 2).MarkUnknown(0x200E, 2).MarkUnknown(0x3006, 2);
        string original = JsonSerializer.Serialize(arena);
        NativeBattleEffectArena result = NativeBattleDiablos325WingMorph.Vertices6590E0(arena, Address + 0x1000, Address + 0x2000, 2048, Address + 0x3000);
        byte[] xyz = result.Read(0x3000, 6);
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(xyz));
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)));
        Assert.Equal((short)255, BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
        Assert.Throws<NotSupportedException>(() => result.Read(0x3006, 1));
        Assert.Equal(original, JsonSerializer.Serialize(arena));
    }

    [Fact]
    public void Polygon_morph_wraps_low_IR_bytes_and_preserves_skipped_counts_indices_and_secondary_color_padding()
    {
        byte[] first = Polygons(255, 4, 1, 0x20);
        byte[] second = Polygons(255, 8, 1, 0x7F);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1000, first).Write(0x2000, second).Write(0x3000, Enumerable.Repeat((byte)0xCC, 108).ToArray());
        arena = arena.MarkUnknown(0x1028, 8).MarkUnknown(0x2028, 8).MarkUnknown(0x3028, 8).MarkUnknown(0x3064, 8).MarkUnknown(0x3043, 1);
        string original = JsonSerializer.Serialize(arena);
        NativeBattleEffectArena result = NativeBattleDiablos325WingMorph.Polygons659200(arena, Address + 0x1000, Address + 0x2000, 2048, Address + 0x3000);
        Assert.Equal(new byte[] { 8, 0, 0, 0, 3, 0, 0, 0 }, result.Read(0x3000, 8));
        Assert.Equal(new byte[] { 0, 6, 0, 0x20 }, result.Read(0x300C, 4));
        Assert.Equal(first.AsSpan(16, 8).ToArray(), result.Read(0x3010, 8));
        Assert.Equal(new byte[] { 0, 6, 0 }, result.Read(0x3040, 3));
        Assert.Throws<NotSupportedException>(() => result.Read(0x3043, 1));
        Assert.Equal((byte)0xCC, result.Read(0x3047, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Read(0x3028, 1));
        Assert.Throws<NotSupportedException>(() => result.Read(0x3064, 1));
        Assert.Equal(original, JsonSerializer.Serialize(arena));
    }

    private static byte[] Vertex(short x, short y, short z)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), z);
        return bytes;
    }

    private static byte[] Polygons(byte red, byte green, byte blue, byte code)
    {
        byte[] bytes = new byte[108];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        int cursor = 8;
        for (int group = 0; group < 6; group++)
        {
            if (group is 2 or 3)
            {
                cursor += 4;
                continue;
            }
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), 1);
            cursor += 4;
            bytes[cursor] = red;
            bytes[cursor + 1] = green;
            bytes[cursor + 2] = blue;
            bytes[cursor + 3] = code;
            for (int index = 0; index < 4; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + index * 2), (ushort)(index * 2));
            }
            int colors = group < 2 ? 1 : group == 4 ? 3 : 4;
            for (int color = 1; color < colors; color++)
            {
                int offset = cursor + 8 + color * 4;
                bytes[offset] = red;
                bytes[offset + 1] = green;
                bytes[offset + 2] = blue;
                bytes[offset + 3] = code;
            }
            cursor += group < 2 ? 12 : group == 4 ? 20 : 24;
        }
        return bytes;
    }
}
