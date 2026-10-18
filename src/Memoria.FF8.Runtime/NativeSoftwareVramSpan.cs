using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>463650's opaque8-bit indexed span over one explicit mutable image and prepared source pointers.</summary>
public static class NativeSoftwareVramSpan
{
    public static void WriteOpaque8Bit463650(byte[] image, int destinationRowByteOffset, int left, int right, int u1024, int stepU1024, int v1024, int stepV1024, int texturePageByteOffset, int paletteByteOffset)
    {
        ArgumentNullException.ThrowIfNull(image);
        uint end = unchecked((uint)(destinationRowByteOffset + 2 * right));
        uint pointer = unchecked((uint)(destinationRowByteOffset + 2 * left));
        uint alignedEnd = end & 0xFFFFFFFCu;
        int u = u1024;
        int v = unchecked(v1024 << 11);
        int dv = unchecked(stepV1024 << 11);
        if ((pointer & 2) != 0)
        {
            ushort color = Sample();
            if (color != 0)
            {
                Store(pointer, color);
                pointer = unchecked(pointer + 2);
            }
        }
        while (pointer < alignedEnd)
        {
            ushort first = Sample();
            ushort second = Sample();
            if (first != 0)
            {
                Store(pointer, first);
            }
            if (second != 0)
            {
                Store(unchecked(pointer + 2), second);
            }
            pointer = unchecked(pointer + 4);
        }
        if (pointer < end)
        {
            ushort color = Sample();
            if (color != 0)
            {
                Store(pointer, color);
            }
        }

        ushort Sample()
        {
            int relative = unchecked((v & 0x1FE00000) + u) >> 10;
            uint address = unchecked((uint)(texturePageByteOffset + relative));
            RequireRange(address, 1);
            byte index = image[(int)address];
            uint palette = unchecked((uint)(paletteByteOffset + 2 * index));
            RequireRange(palette, 2);
            ushort color = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan((int)palette, 2));
            u = unchecked(u + stepU1024);
            v = unchecked(v + dv);
            return color;
        }
        void Store(uint address, ushort color)
        {
            RequireRange(address, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan((int)address, 2), color);
        }
        void RequireRange(uint address, int length)
        {
            if ((ulong)address + (uint)length > (ulong)image.Length)
            {
                throw new NotSupportedException("Native opaque8-bit span access leaves its explicitly supplied mutable memory owner.");
            }
        }
    }
}
