using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325BodyPackets
{
    public static NativeBattleGfGeometryAttempt? Emit6576A0(ReadOnlySpan<byte> polygon, bool quad, Func<int, NativeBattleProjectedVertex> readVertex, uint rgb, short zsf, int byteOffset)
    {
        if (polygon.Length != (quad ? 20 : 16))
        {
            throw new InvalidDataException("Native Diablos body requires its current plain triangle or quad record.");
        }
        int count = quad ? 4 : 3;
        NativeBattleProjectedVertex[] vertices = new NativeBattleProjectedVertex[count];
        ushort depth = 0;
        byte anyShade = 0;
        byte sharedShade = byte.MaxValue;
        byte sharedClip = byte.MaxValue;
        uint sum = 0;
        for (int index = 0; index < count; index++)
        {
            int source = BinaryPrimitives.ReadUInt16LittleEndian(polygon[(index * 2)..]) & 0xFFF;
            NativeBattleProjectedVertex vertex = readVertex(source);
            vertices[index] = vertex;
            depth |= vertex.Depth;
            byte shade = (byte)(vertex.DepthAndClip >> 24);
            anyShade |= shade;
            sharedShade &= shade;
            sharedClip &= (byte)(vertex.DepthAndClip >> 16);
            sum += vertex.Depth;
        }
        if (depth == 0 || anyShade == 0 || sharedClip != 0 || NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0)
        {
            return null;
        }
        uint average = unchecked(sum * (uint)(int)zsf) >> 12;
        int bucket = unchecked(17 + (int)average);
        bool flat = sharedShade >= 64;
        int length = flat ? quad ? 40 : 32 : quad ? 52 : 40;
        SkinWrites packet = new(length, byteOffset);
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        if (flat)
        {
            uint transparent = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(polygon[14..]) & 0x200) << 16;
            packet.Word(4, rgb | (quad ? 0x2C000000u : 0x24000000u) | transparent);
            for (int index = 0; index < count; index++)
            {
                packet.Word(8 + index * 8, vertices[index].Screen);
            }
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(polygon[8..]));
            packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(polygon[12..]) & 0xFDFFFFFF);
            packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(polygon[(quad ? 16 : 6)..]));
            if (quad)
            {
                packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(polygon[18..]));
            }
        }
        else
        {
            for (int index = 0; index < count; index++)
            {
                uint shade = vertices[index].DepthAndClip >> 24;
                uint color = shade * 0x010101;
                if (index == 0)
                {
                    packet.Word(4, color | (quad ? 0x3C000000u : 0x34000000u));
                }
                else
                {
                    packet.Word(4 + index * 12, color);
                }
                packet.Word(8 + index * 12, vertices[index].Screen);
            }
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(polygon[8..]));
            packet.Word(24, BinaryPrimitives.ReadUInt32LittleEndian(polygon[12..]) & 0xFDFFFFFF);
            packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(polygon[(quad ? 16 : 6)..]));
            if (quad)
            {
                packet.Word16(48, BinaryPrimitives.ReadUInt16LittleEndian(polygon[18..]));
            }
        }
        return packet.Attempt(new(bucket, packet.Words(), [0, 0, 0, 0]));
    }
}
