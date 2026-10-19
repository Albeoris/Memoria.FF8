using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>796720's radial per-vertex reveal and plain textured Gouraud triangle/quad packets.</summary>
public static class NativeBattleAngelWing84ShaderPackets
{
    public static byte Shade(NativeBattleVector position, short centerY, short radius)
    {
        int dy = position.Y - centerY;
        int square = unchecked(position.X * position.X + dy * dy);
        int distance = unchecked((int)(long)Math.Sqrt(square));
        int intensity = distance > radius - 128 ? distance > radius ? 0 : radius - distance : 128;
        uint high = unchecked((uint)(((ulong)2863311532u * (uint)intensity) >> 32));
        return unchecked((byte)((high >> 31) + high));
    }

    public static NativeBattleGfGeometryBatch Emit(ReadOnlySpan<byte> polygon, bool quad, IReadOnlyList<NativeBattleModuleProjectedVertex> corners, IReadOnlyList<byte> shades)
    {
        if (polygon.Length != (quad ? 20 : 16) || corners.Count < (quad ? 4 : 3) || shades.Count < (quad ? 4 : 3))
        {
            throw new InvalidDataException("Native796720 requires its original plain polygon and complete vertex input owners.");
        }
        NativeBattleModuleProjectedVertex a = corners[0];
        NativeBattleModuleProjectedVertex b = corners[1];
        NativeBattleModuleProjectedVertex c = corners[2];
        if (NativeBattleModulePackets.Nclip(a.Screen, b.Screen, c.Screen) <= 0)
        {
            return new([], 0);
        }
        NativeBattleModuleProjectedVertex[] selected = quad ? [a, b, c, corners[3]] : [a, b, c];
        int length = quad ? 52 : 40;
        byte[] bytes = new byte[length];
        NativeBattleGfPacketByteState[] known = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, length).ToArray();
        Dword(0, quad ? 0x0C000000u : 0x09000000u);
        bytes[7] = quad ? (byte)0x3E : (byte)0x36;
        known[7] = NativeBattleGfPacketByteState.Known;
        for (int index = 0; index < selected.Length; index++)
        {
            int colorOffset = 4 + index * 12;
            for (int channel = 0; channel < 3; channel++)
            {
                bytes[colorOffset + channel] = shades[index];
                known[colorOffset + channel] = NativeBattleGfPacketByteState.Known;
            }
            Dword(8 + index * 12, selected[index].Screen);
        }
        Dword(12, BinaryPrimitives.ReadUInt32LittleEndian(polygon.Slice(8)));
        Dword(24, BinaryPrimitives.ReadUInt32LittleEndian(polygon.Slice(12)));
        Word(36, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(quad ? 16 : 6)));
        if (quad)
        {
            Word(48, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(18)));
        }
        uint sum = unchecked((uint)selected.Sum(vertex => (int)vertex.Depth));
        uint average = unchecked(sum * (quad ? 0x100u : 0x155u)) >> 12;
        int bucket = checked(17 + (int)(average >> 2));
        ImmutableArray<int> payload = quad ? [a.Depth, b.Depth, c.Depth, selected[3].Depth] : [a.Depth, b.Depth, c.Depth, 0];
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        ImmutableArray<NativeBattleGfPacketByteState> mask = known.ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, payload) { PhysicalWriteMask = mask };
        return new([new(0, bytes.ToImmutableArray(), mask, primitive)], length);

        void Dword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(known, NativeBattleGfPacketByteState.Known, offset, 4);
        }
        void Word(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(known, NativeBattleGfPacketByteState.Known, offset, 2);
        }
    }
}
