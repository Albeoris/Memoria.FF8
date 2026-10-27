using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38GroundEntries
{
    public static ImmutableArray<byte> Build(short primitive, short vertexBase, NativeBattleRawPolygon polygon, ImmutableArray<byte> workspace, ImmutableArray<byte> retained)
    {
        if (workspace.IsDefault || retained.IsDefault || retained.Length != 112)
        {
            throw new InvalidDataException("Native Quake split mesh requires its retained 112-byte entry and posed workspace.");
        }
        byte[] result = retained.ToArray();
        BuildCore(primitive, vertexBase, polygon, workspace.AsSpan(), result);
        return result.ToImmutableArray();
    }

    public static NativeBattleEffectArena Build(NativeBattleEffectArena arena, int entryOffset, short primitive, short vertexBase, NativeBattleRawPolygon polygon, ReadOnlySpan<byte> workspace)
    {
        if (entryOffset < 0 || entryOffset > NativeBattleEffectArena.Length - 112)
        {
            throw new InvalidDataException("Native Quake split entry leaves its actual ground bank.");
        }
        Span<byte> result = stackalloc byte[112];
        BuildCore(primitive, vertexBase, polygon, workspace, result);
        Span<bool> written = stackalloc bool[112];
        written.Clear();
        int count = polygon.Quad ? 4 : 3;
        for (int index = 0; index <= count; index++)
        {
            written.Slice(index * 8, 6).Fill(true);
            written.Slice(52 + index * 2, 2).Fill(true);
            written[100 + index] = true;
            written[105 + index] = true;
        }
        written.Slice(40, 12).Fill(true);
        written[110] = true;
        using NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < written.Length;)
        {
            if (!written[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < written.Length && written[index])
            {
                index++;
            }
            writer.Write(entryOffset + first, result[first..index]);
        }
        return writer.Freeze();
    }

    private static void BuildCore(short primitive, short vertexBase, NativeBattleRawPolygon polygon, ReadOnlySpan<byte> workspace, Span<byte> result)
    {
        int count = polygon.Quad ? 4 : 3;
        if (polygon.Bytes.IsDefault || polygon.Bytes.Length != (polygon.Quad ? 24 : 20) || result.Length != 112)
        {
            throw new InvalidDataException("Native Quake split mesh requires its original unconverted polygon, posed vertex workspace and retained 112-byte entry.");
        }
        for (int index = 0; index < count; index++)
        {
            int source = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(index * 2)) & 0xFFF;
            if (checked(source * 8 + 6) > workspace.Length)
            {
                throw new InvalidDataException("Native Quake polygon exceeds its posed vertex workspace.");
            }
        }
        int x = 0;
        int y = 0;
        int z = 0;
        int u = 0;
        int v = 0;
        for (int index = 0; index < count; index++)
        {
            int source = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(index * 2)) & 0xFFF;
            if (checked(source * 8 + 6) > workspace.Length)
            {
                throw new InvalidDataException("Native Quake polygon exceeds its posed vertex workspace.");
            }
            short px = BinaryPrimitives.ReadInt16LittleEndian(workspace.Slice(source * 8));
            short py = BinaryPrimitives.ReadInt16LittleEndian(workspace.Slice(source * 8 + 2));
            short pz = BinaryPrimitives.ReadInt16LittleEndian(workspace.Slice(source * 8 + 4));
            StoreWord(result, index * 8, px);
            StoreWord(result, index * 8 + 2, py);
            StoreWord(result, index * 8 + 4, pz);
            StoreWord(result, 52 + index * 2, unchecked((short)(source + vertexBase)));
            int uv = index == count - 1 ? (count == 3 ? 6 : 18) : 8 + index * 4;
            result[100 + index] = polygon.Bytes[uv];
            result[105 + index] = polygon.Bytes[uv + 1];
            x += px;
            y += py;
            z += pz;
            u += polygon.Bytes[uv];
            v += polygon.Bytes[uv + 1];
        }
        StoreWord(result, count * 8, unchecked((short)(x / count)));
        StoreWord(result, count * 8 + 2, unchecked((short)(y / count)));
        StoreWord(result, count * 8 + 4, unchecked((short)(z / count)));
        StoreWord(result, 52 + count * 2, unchecked((short)(primitive + 400)));
        result[100 + count] = (byte)(u / count);
        result[105 + count] = (byte)(v / count);
        polygon.Bytes.AsSpan().Slice(14, 2).CopyTo(result[48..]);
        polygon.Bytes.AsSpan().Slice(10, 2).CopyTo(result[50..]);
        for (int channel = 0; channel < 3; channel++)
        {
            byte color = polygon.Bytes[(count == 3 ? 16 : 20) + channel];
            result[40 + channel] = color;
            result[44 + channel] = (byte)(3 * color / 5);
        }
        result[43] = 0x24;
        result[47] = 0x2C;
        result[110] = (byte)(count - 2);
    }

    private static void StoreWord(Span<byte> bytes, int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes[offset..], value);
    }
}
