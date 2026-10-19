using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>466EF0 writes an absolute VRAM rectangle without polygon clipping or drawing-state transforms.</summary>
public static class NativeSoftwareRectangleFill
{
    public static bool Fill4623A0(byte[] image, ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 16 || packet[7] != 0x02)
        {
            throw new InvalidDataException("Native normal rectangle fill requires its complete fill packet.");
        }
        int x = BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(8));
        int y = BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(10));
        return x >= 640 && y >= 240 && Fill466EF0(image, packet);
    }

    public static bool Fill466EF0(byte[] image, ReadOnlySpan<byte> packet)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Length != 1024 * 512 * 2 || packet.Length < 16 || packet[7] != 0x02)
        {
            throw new InvalidDataException("Native rectangle fill requires its actual VRAM image and complete fill packet.");
        }
        int x = BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(8));
        int y = BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(10));
        if (x < 0 || y < 0)
        {
            return false;
        }
        int width = BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(12));
        int height = BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(14));
        if (x + width > 1024 || y + height > 512)
        {
            return false;
        }
        if (width < 0 || height < 0)
        {
            throw new NotSupportedException("Native rectangle fill with a negative extent has no bounded image-write contract.");
        }
        ushort color = unchecked((ushort)((packet[4] >> 3) | ((packet[5] & 0xF8) << 2) | ((packet[6] & 0xF8) << 7)));
        uint pair = color | ((uint)color << 16);
        for (int row = 0; row < height; row++)
        {
            int offset = 2 * ((y + row) * 1024 + x);
            for (int column = 0; column < width / 2; column++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + column * 4), pair);
            }
            if ((width & 1) != 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset + (width - 1) * 2), color);
            }
        }
        return width != 0 && height != 0;
    }
}
