using System.Buffers.Binary;
using Albeoris.Games.FF8.TextEncoding;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private readonly Dictionary<(int Window, int Message), NativeWorldMessage> worldMessageBindings = [];

    public NativeWorldMessage ReadNativeWorldMessage(int window, int message)
    {
        if (window is < 0 or > 12 || message is < 0 or > 255)
        {
            throw new NotSupportedException("World message binding requires an original window0..12 and unsigned message BYTE.");
        }
        if (worldMessageBindings.TryGetValue((window, message), out NativeWorldMessage? cached))
        {
            return cached;
        }
        _ = ReadWorldMessage(message);
        ReadOnlySpan<byte> record = NativeWorldMemory.Read(0xC761A0u + (uint)window * 16, 8);
        NativeWorldMessage result = new(window, (byte)message, record[2], record[1], record[3], BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadInt16LittleEndian(record[6..]), Convert.ToHexString(SHA256.HashData(wmset)), false);
        result.Validate();
        worldMessageBindings.Add((window, message), result);
        return result;
    }

    public string ReadWorldMessage(int message)
    {
        byte[] bytes = ReadWorldMessageBytes(message);
        return FF8Encoding.CreateEuropean().GetString(bytes.AsSpan(0, bytes.Length - 1));
    }

    public short ReadWorldMessageSpeed(byte speed)
    {
        if (speed > 4)
        {
            throw new NotSupportedException("World message speed requires the original settings index0..4.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(NativeWorldMemory.Read(0xB8EE94u + (uint)speed * 2, 2));
    }

    public byte[] ReadWorldMessageBytes(int message)
    {
        //542DA0 binds20400C4 to zero-based section13;5438D0 reads its relative DWORD offsets.
        ReadOnlySpan<byte> section = wmset.AsSpan(sections[13], sections[14] - sections[13]);
        int tableBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section));
        if (tableBytes < 4 || tableBytes % 4 != 0 || tableBytes > section.Length || message < 0 || message >= tableBytes / 4)
        {
            throw new InvalidDataException("Original world message index or pointer table is invalid.");
        }
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section.Slice(message * 4, 4)));
        if (start < tableBytes || start >= section.Length)
        {
            throw new InvalidDataException("Original world message pointer is outside its text section.");
        }
        int end = section.Slice(start).IndexOf((byte)0);
        if (end < 0)
        {
            throw new InvalidDataException("Original world message has no bounded text terminator.");
        }
        return section.Slice(start, end + 1).ToArray();
    }
}
