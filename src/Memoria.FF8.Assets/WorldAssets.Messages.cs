using System.Buffers.Binary;
using Albeoris.Games.FF8.TextEncoding;
using System.Reflection.PortableExecutable;
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
        byte[] executable = contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new InvalidDataException("World message windows require their authenticated original executable table.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        int rva = checked((int)(0xC761A0 - reader.PEHeaders.PEHeader!.ImageBase) + 16 * window);
        SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 8 <= section.VirtualAddress + section.SizeOfRawData);
        ReadOnlySpan<byte> record = executable.AsSpan(rva - section.VirtualAddress + section.PointerToRawData, 8);
        NativeWorldMessage result = new(window, (byte)message, record[2], record[1], record[3], BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadInt16LittleEndian(record[6..]), Convert.ToHexString(SHA256.HashData(wmset)), false);
        result.Validate();
        worldMessageBindings.Add((window, message), result);
        return result;
    }

    public string ReadWorldMessage(int message)
    {
        //542DA0 binds20400C4 to zero-based section13;5438D0 reads its relative DWORD offsets.
        ReadOnlySpan<byte> section = wmset.AsSpan(sections[13], sections[14] - sections[13]);
        int tableBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section));
        if (tableBytes < 4 || tableBytes % 4 != 0 || tableBytes > section.Length || message < 0 || message >= tableBytes / 4)
        {
            throw new InvalidDataException("Original world message index or pointer table is invalid.");
        }
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section.Slice(message * 4, 4)));
        int end = section.Slice(start).IndexOf((byte)0);
        if (start < tableBytes || end < 0)
        {
            throw new InvalidDataException("Original world message has no bounded text terminator.");
        }
        return FF8Encoding.CreateEuropean().GetString(section.Slice(start, end));
    }
}
