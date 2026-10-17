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
        ImmutableArray<NativeBattleVector> vertices = MorphVertices701390(source, geometryOffset, firstShape, secondShape, ratio);
        byte[] output = destination.ToArray();
        foreach (NativeBattleVector vertex in vertices)
        {
            if (destinationOffset < 0 || destinationOffset > output.Length - 6)
            {
                throw new InvalidDataException("Native701390 XYZ store exceeds its observed destination arena bytes.");
            }
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(destinationOffset), vertex.X);
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(destinationOffset + 2), vertex.Y);
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(destinationOffset + 4), vertex.Z);
            destinationOffset = unchecked(destinationOffset + 8);
        }
        return output.ToImmutableArray();
    }

    public static ImmutableArray<NativeBattleVector> MorphVertices701390(ImmutableArray<byte> source, int geometryOffset, int firstShape, int secondShape, int ratio)
    {
        if (source.IsDefault)
        {
            throw new InvalidDataException("Native701390 requires its current source geometry.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(source, checked(geometryOffset + 4), 4));
        if (count < 0 || count > source.Length / 8)
        {
            throw new InvalidDataException("Native701390 vertex count exceeds its bounded observed source.");
        }
        ImmutableArray<NativeBattleVector>.Builder result = ImmutableArray.CreateBuilder<NativeBattleVector>(count);
        int first = unchecked(geometryOffset + 12 + count * firstShape * 8);
        int second = unchecked(geometryOffset + 12 + count * secondShape * 8);
        short firstWeight = unchecked((short)(4096 - ratio));
        short secondWeight = unchecked((short)ratio);
        for (int vertex = 0; vertex < count; vertex++)
        {
            short[] xyz = new short[3];
            for (int component = 0; component < 3; component++)
            {
                int offset = component * 2;
                short a = BinaryPrimitives.ReadInt16LittleEndian(Read(source, unchecked(first + offset), 2));
                short b = BinaryPrimitives.ReadInt16LittleEndian(Read(source, unchecked(second + offset), 2));
                // 45E9D0 stores the first rounded product in MAC; 45EBF0 adds a separately rounded product before IR saturation.
                int mac = unchecked(NativePrecision53.RoundInteger(a * (firstWeight / 4096.0)) + NativePrecision53.RoundInteger(b * (secondWeight / 4096.0)));
                xyz[component] = (short)Math.Clamp(mac, short.MinValue, short.MaxValue);
            }
            result.Add(new(xyz[0], xyz[1], xyz[2]));
            first = unchecked(first + 8);
            second = unchecked(second + 8);
        }
        return result.MoveToImmutable();
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
