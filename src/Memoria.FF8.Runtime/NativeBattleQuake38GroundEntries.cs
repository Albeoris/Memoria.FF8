using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38GroundEntries
{
    public static ImmutableArray<byte> Build(short primitive, short vertexBase, NativeBattleRawPolygon polygon, ImmutableArray<byte> workspace, ImmutableArray<byte> retained)
    {
        int count = polygon.Quad ? 4 : 3;
        if (polygon.Bytes.IsDefault || polygon.Bytes.Length != (polygon.Quad ? 24 : 20) || workspace.IsDefault || retained.IsDefault || retained.Length != 112)
        {
            throw new InvalidDataException("Native Quake split mesh requires its original unconverted polygon, posed vertex workspace and retained112-byte entry.");
        }
        byte[] result = retained.ToArray();
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
            short px = BinaryPrimitives.ReadInt16LittleEndian(workspace.AsSpan().Slice(source * 8));
            short py = BinaryPrimitives.ReadInt16LittleEndian(workspace.AsSpan().Slice(source * 8 + 2));
            short pz = BinaryPrimitives.ReadInt16LittleEndian(workspace.AsSpan().Slice(source * 8 + 4));
            Word(index * 8, px);
            Word(index * 8 + 2, py);
            Word(index * 8 + 4, pz);
            Word(52 + index * 2, unchecked((short)(source + vertexBase)));
            int uv = index == count - 1 ? (count == 3 ? 6 : 18) : 8 + index * 4;
            result[100 + index] = polygon.Bytes[uv];
            result[105 + index] = polygon.Bytes[uv + 1];
            x += px;
            y += py;
            z += pz;
            u += polygon.Bytes[uv];
            v += polygon.Bytes[uv + 1];
        }
        Word(count * 8, unchecked((short)(x / count)));
        Word(count * 8 + 2, unchecked((short)(y / count)));
        Word(count * 8 + 4, unchecked((short)(z / count)));
        Word(52 + count * 2, unchecked((short)(primitive + 400)));
        result[100 + count] = (byte)(u / count);
        result[105 + count] = (byte)(v / count);
        polygon.Bytes.AsSpan().Slice(14, 2).CopyTo(result.AsSpan(48));
        polygon.Bytes.AsSpan().Slice(10, 2).CopyTo(result.AsSpan(50));
        for (int channel = 0; channel < 3; channel++)
        {
            byte color = polygon.Bytes[(count == 3 ? 16 : 20) + channel];
            result[40 + channel] = color;
            result[44 + channel] = (byte)(3 * color / 5);
        }
        result[43] = 0x24;
        result[47] = 0x2C;
        result[110] = (byte)(count - 2);
        return result.ToImmutableArray();

        void Word(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(offset), value);
    }
}
