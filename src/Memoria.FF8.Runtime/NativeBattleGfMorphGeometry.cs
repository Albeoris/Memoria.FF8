using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>701390's two separately rounded PC GTE products and XYZ-only writes into caller-owned arena bytes.</summary>
public static class NativeBattleGfMorphGeometry
{
    public static ImmutableArray<byte> Morph701390(ImmutableArray<byte> source, int geometryOffset, int firstShape, int secondShape, int ratio, ImmutableArray<byte> destination, int destinationOffset = 0)
    {
        if (source.IsDefault || destination.IsDefault)
        {
            throw new InvalidDataException("Native701390 requires current source geometry and actual destination arena bytes.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(source, checked(geometryOffset + 4), 4));
        if (count < 0)
        {
            throw new InvalidDataException("Native701390 vertex count exceeds its bounded observed source and destination.");
        }
        if (count == 0)
        {
            return destination;
        }
        int first = unchecked(geometryOffset + 12 + count * firstShape * 8);
        int second = unchecked(geometryOffset + 12 + count * secondShape * 8);
        short firstWeight = unchecked((short)(4096 - ratio));
        short secondWeight = unchecked((short)ratio);
        byte[] output = destination.ToArray();
        for (int vertex = 0; vertex < count; vertex++)
        {
            for (int component = 0; component < 3; component++)
            {
                int offset = component * 2;
                short a = BinaryPrimitives.ReadInt16LittleEndian(Read(source, unchecked(first + offset), 2));
                short b = BinaryPrimitives.ReadInt16LittleEndian(Read(source, unchecked(second + offset), 2));
                // 45E9D0 stores the first rounded product in MAC; 45EBF0 adds a separately rounded product before IR saturation.
                int mac = unchecked(NativePrecision53.RoundInteger(a * (firstWeight / 4096.0)) + NativePrecision53.RoundInteger(b * (secondWeight / 4096.0)));
                int target = unchecked(destinationOffset + offset);
                if (target < 0 || target > output.Length - 2)
                {
                    throw new InvalidDataException("Native701390 XYZ store exceeds its observed destination arena bytes.");
                }
                BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(target, 2), (short)Math.Clamp(mac, short.MinValue, short.MaxValue));
            }
            first = unchecked(first + 8);
            second = unchecked(second + 8);
            destinationOffset = unchecked(destinationOffset + 8);
        }
        return output.ToImmutableArray();
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (offset < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Native701390 load exceeds its observed current geometry bytes.");
        }
        return bytes.AsSpan().Slice(offset, length);
    }
}
