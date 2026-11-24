using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Native54F920's ordered section34 lookup, selected by the low five bits of field byte266.</summary>
public static class WorldDrawPointReader
{
    public static int Find(ReadOnlySpan<byte> section, ReadOnlySpan<byte> selectors, int cell, int polygonIndex)
    {
        if (selectors.Length != 4 || section.Length < 44)
        {
            throw new InvalidDataException("World draw-point lookup requires four selectors and its relative record table.");
        }
        int origin = BinaryPrimitives.ReadInt32LittleEndian(section[40..]);
        foreach (byte raw in selectors)
        {
            int selector = unchecked((sbyte)raw);
            if (selector < 0)
            {
                return -1;
            }
            int pair = selector * 8;
            if (pair > section.Length - 8)
            {
                throw new InvalidDataException("World draw-point selector lies outside its table.");
            }
            int start = BinaryPrimitives.ReadInt32LittleEndian(section[pair..]);
            int end = BinaryPrimitives.ReadInt32LittleEndian(section[(pair + 4)..]);
            if (start < 0 || end < start || end > section.Length || (end - start) % 4 != 0)
            {
                throw new InvalidDataException($"World draw-point records lie outside their section: selector {selector}, start {start}, end {end}, length {section.Length}.");
            }
            for (int offset = start; offset < end; offset += 4)
            {
                if (BinaryPrimitives.ReadInt16LittleEndian(section[offset..]) == cell && section[offset + 2] == polygonIndex)
                {
                    return (offset - origin) >> 2;
                }
            }
        }
        return -1;
    }
}

public sealed partial class WorldAssets
{
    public (uint Id, uint Operation, uint Pan, uint Volume) ReadWorldDrawPointSound(bool acquire)
    {
        int slot = acquire ? 11 : 10;
        ReadOnlySpan<byte> entry = NativeWorldMemory.Read(0xC75F18u + (uint)slot * 20, 18);
        if (BinaryPrimitives.ReadUInt32LittleEndian(entry) != 0)
        {
            throw new InvalidDataException("World Draw sound requires a direct effect selector.");
        }
        return (BinaryPrimitives.ReadUInt16LittleEndian(entry[16..]), BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]), BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]), BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]));
    }

    public int FindNativeDrawPoint(NativeWorldGroundIdentity ground, byte storyByte266)
    {
        ground.Validate();
        ReadOnlySpan<byte> selectors = NativeWorldMemory.Read(0xC76770u + 4u * (uint)(storyByte266 & 31), 4);
        return WorldDrawPointReader.Find(wmset.AsSpan(sections[34], sections[35] - sections[34]), selectors, ground.Cell, ground.PolygonIndex);
    }
}
