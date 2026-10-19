using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>The shared45BB00 forward VRAM transfer used by software and normal image commands.</summary>
public static class NativeSoftwareImageTransfer
{
    public static bool Transfer45BB00(byte[] image, ReadOnlySpan<byte> packet, Action<int, int, int, int> invalidate)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(invalidate);
        if (image.Length != 1024 * 512 * 2 || packet.Length < 20 || packet[7] != 0x80)
        {
            throw new InvalidDataException("Native image transfer requires its complete packet and shared VRAM image.");
        }
        uint source = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(8));
        uint destination = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(12));
        uint extent = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(16));
        int sourceX = (int)(source & 0x3FF);
        int sourceY = (int)((source >> 16) & 0x1FF);
        int destinationX = (int)(destination & 0x3FF);
        int destinationY = (int)((destination >> 16) & 0x1FF);
        int width = Math.Min(Math.Min((int)(extent & 0xFFFF), 1024 - sourceX), 1024 - destinationX);
        int height = Math.Min(Math.Min((int)(extent >> 16), 512 - sourceY), 512 - destinationY);
        int rowBytes = width * 2;
        for (int row = 0; row < height; row++)
        {
            int from = 2 * ((sourceY + row) * 1024 + sourceX);
            int to = 2 * ((destinationY + row) * 1024 + destinationX);
            int column = 0;
            for (; column + 4 <= rowBytes; column += 4)
            {
                uint value = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(from + column));
                BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(to + column), value);
            }
            for (; column < rowBytes; column++)
            {
                image[to + column] = image[from + column];
            }
        }
        invalidate(destinationX, destinationY, destinationX + width - 1, destinationY + height - 1);
        return width != 0 && height != 0;
    }
}
