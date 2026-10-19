using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>463650's opaque8-bit indexed span over one explicit mutable image and prepared source pointers.</summary>
public static class NativeSoftwareVramSpan
{
    public static void WriteTextured(byte[] image, int destinationRowByteOffset, int left, int right, int u1024, int stepU1024, int v1024, int stepV1024, int texturePageByteOffset, int paletteByteOffset, byte flags, byte variant, int depth)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateDispatch(flags, variant, depth);
        int mode = (flags & 4) == 0 ? 0 : variant;
        if (depth == 1 && mode is 0 or 3)
        {
            WriteOpaque8Bit463650(image, destinationRowByteOffset, left, right, u1024, stepU1024, v1024, stepV1024, texturePageByteOffset, paletteByteOffset);
            return;
        }
        uint pointer = unchecked((uint)(destinationRowByteOffset + 2 * left));
        uint end = unchecked((uint)(destinationRowByteOffset + 2 * right));
        int vShift = 12 - depth;
        int v = unchecked(v1024 << vShift);
        int dv = unchecked(stepV1024 << vShift);
        int u = u1024;
        uint mask = depth == 0 ? 0x3FC00000u : depth == 1 ? 0x1FE00000u : 0x0FF00000u;
        int addressShift = depth == 0 ? 11 : 10;
        while (mode == 1 ? pointer < end : pointer <= end)
        {
            int relative = unchecked((int)((uint)v & mask) + u) >> addressShift;
            byte packed = ReadByte(unchecked((uint)(texturePageByteOffset + relative)));
            ushort color = depth == 2 ? packed : ReadWord(unchecked((uint)(paletteByteOffset + 2 * (depth == 0 ? (packed >> ((u >> 8) & 4)) & 15 : packed))));
            if (color != 0)
            {
                if (mode is 1 or 2)
                {
                    ushort previous = ReadWord(pointer);
                    int combined = 0;
                    foreach (int channelMask in ChannelMasks)
                    {
                        int channel = mode == 1 ? Math.Min((previous & channelMask) + (color & channelMask), channelMask) : Math.Max((previous & channelMask) - (color & channelMask), 0);
                        combined |= channel;
                    }
                    color = unchecked((ushort)combined);
                }
                RequireRange(pointer, 2);
                BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan((int)pointer, 2), color);
            }
            pointer = unchecked(pointer + 2);
            u = unchecked(u + stepU1024);
            v = unchecked(v + dv);
        }

        byte ReadByte(uint address)
        {
            RequireRange(address, 1);
            return image[(int)address];
        }

        ushort ReadWord(uint address)
        {
            RequireRange(address, 2);
            return BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan((int)address, 2));
        }

        void RequireRange(uint address, int length)
        {
            if ((ulong)address + (uint)length > (ulong)image.Length)
            {
                throw new NotSupportedException("Native textured span access leaves its supplied mutable memory owner.");
            }
        }
    }

    private static readonly int[] ChannelMasks = [0x1F, 0x3E0, 0x7C00];

    public static void ValidateDispatch(byte flags, byte variant, int depth)
    {
        if ((uint)depth >= 3 || (flags & 4) != 0 && variant >= 4)
        {
            throw new NotSupportedException("Native textured span dispatch index leaves its supplied original table.");
        }
    }

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
