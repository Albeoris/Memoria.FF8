using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original4B76A0 ordinary ICON.SP1 parts; records are consumed by49BB30, not the battle OT.</summary>
public static class NativeMenuIconPackets
{
    public static ImmutableArray<byte> Encode4B76A0(ReadOnlySpan<byte> parts, int x, int y, uint color, int paletteOffset)
    {
        if (parts.Length % 8 != 0)
        {
            throw new InvalidDataException("Native4B76A0 requires complete eight-byte ICON.SP1 parts.");
        }
        byte[] packets = new byte[checked(parts.Length / 8 * 24)];
        uint mode = 0xE100041Eu | ((color >> 24 & 3) << 5);
        uint command = 0x64000000u | (color & 0xFFFFFF) | ((color >> 26 & 1) << 25);
        uint palette = unchecked((uint)(paletteOffset + 0x3810) << 16);
        for (int offset = 0; offset < parts.Length; offset += 8)
        {
            ReadOnlySpan<byte> part = parts.Slice(offset, 8);
            Span<byte> packet = packets.AsSpan(offset / 8 * 24, 24);
            BinaryPrimitives.WriteUInt32LittleEndian(packet, 0x05000000);
            BinaryPrimitives.WriteUInt32LittleEndian(packet[4..], mode);
            BinaryPrimitives.WriteUInt32LittleEndian(packet[8..], command);
            BinaryPrimitives.WriteUInt16LittleEndian(packet[12..], unchecked((ushort)(x + (sbyte)part[5])));
            BinaryPrimitives.WriteUInt16LittleEndian(packet[14..], unchecked((ushort)(y + (sbyte)part[7])));
            BinaryPrimitives.WriteUInt32LittleEndian(packet[16..], unchecked((BinaryPrimitives.ReadUInt32LittleEndian(part) & 0x07CFFFFFu) + palette));
            BinaryPrimitives.WriteUInt32LittleEndian(packet[20..], BinaryPrimitives.ReadUInt32LittleEndian(part[4..]) & 0x00FF00FFu);
        }
        return ImmutableArray.CreateRange(packets);
    }
}
