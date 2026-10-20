using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38GroundZoneWrite(ImmutableArray<byte> Entry, ImmutableArray<byte> VertexZones);

public static class NativeBattleQuake38GroundZones
{
    public static NativeBattleQuake38GroundZoneWrite Assign8D9950(ImmutableArray<byte> entry, ImmutableArray<byte> vertexZones, int edge, int first, int second, int centroid, NativeBattleQuake38Resources resources, short halfX, short halfZ, short offsetZ)
    {
        if (entry.IsDefault || entry.Length != 112 || vertexZones.IsDefault || vertexZones.Length != 0xCC0 || (uint)edge >= 4 || (uint)first >= 5 || (uint)second >= 5 || (uint)centroid >= 5)
        {
            throw new InvalidDataException("Native Quake zone assignment requires its actual split entry, edge selectors and vertex-zone bank.");
        }
        byte[] result = entry.ToArray();
        byte[] masks = vertexZones.ToArray();
        short x = unchecked((short)((Word(first * 8) + Word(second * 8) + Word(centroid * 8)) / 3));
        short z = unchecked((short)((Word(first * 8 + 4) + Word(second * 8 + 4) + Word(centroid * 8 + 4)) / 3));
        byte zone = NativeBattleQuake38Zones.Read8D9A80(resources.ZoneMap, x, z, halfX, halfZ, offsetZ);
        result[88 + edge] = zone;
        result[92 + edge] = NativeBattleQuake38Zones.Read8D9A80(resources.DelayMap, x, z, halfX, halfZ, offsetZ);
        ushort bit = unchecked((ushort)(1 << zone));
        for (int pass = 0; pass < 5; pass++)
        {
            Mark(first);
            Mark(second);
            Mark(centroid);
        }
        return new(result.ToImmutableArray(), masks.ToImmutableArray());

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(result.AsSpan(offset));
        void Mark(int selector)
        {
            int index = Word(52 + selector * 2);
            int offset = checked(index * 4);
            if (offset < 0 || offset + 4 > masks.Length)
            {
                throw new InvalidDataException("Native Quake vertex-zone index leaves its original bank.");
            }
            ushort mask = BinaryPrimitives.ReadUInt16LittleEndian(masks.AsSpan(offset));
            if ((mask & bit) == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(masks.AsSpan(offset), (ushort)(mask | bit));
                ushort count = BinaryPrimitives.ReadUInt16LittleEndian(masks.AsSpan(offset + 2));
                BinaryPrimitives.WriteUInt16LittleEndian(masks.AsSpan(offset + 2), unchecked((ushort)(count + 1)));
            }
        }
    }
}
