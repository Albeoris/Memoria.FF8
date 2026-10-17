using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeVibrationBankReader
{
    public static NativeVibrationBank ReadExecutable(byte[] image)
    {
        string hash = Convert.ToHexString(SHA256.HashData(image));
        if (hash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Vibration patterns require the authenticated original English executable.");
        }
        using var reader = new PEReader(new MemoryStream(image));
        long rva = 0xB8DCB0 - (long)reader.PEHeaders.PEHeader!.ImageBase;
        SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 412 <= section.VirtualAddress + section.SizeOfRawData);
        int start = checked((int)(rva - section.VirtualAddress + section.PointerToRawData));
        return Read(image.AsSpan(start, 412), hash);
    }

    public static NativeVibrationBank ReadWorld(byte[] wmset)
    {
        if (wmset.Length < 0x7C)
        {
            throw new InvalidDataException("World vibration bank requires the original wmset header.");
        }
        //542DA0 binds base+DWORD(header74).550070 indexes1 even though
        //the named section contains only DWORD0: the next physical DWORD
        //and its relative payload still belong to the loaded wmset buffer.
        int start = BinaryPrimitives.ReadInt32LittleEndian(wmset.AsSpan(0x74));
        if (start < 0x7C || start > wmset.Length - 8)
        {
            throw new InvalidDataException("World vibration bank offsets lie outside wmset.");
        }
        NativeVibrationBank bank = new(Convert.ToHexString(SHA256.HashData(wmset)), ImmutableArray.Create(BinaryPrimitives.ReadInt32LittleEndian(wmset.AsSpan(start)), BinaryPrimitives.ReadInt32LittleEndian(wmset.AsSpan(start + 4))), ImmutableArray.CreateRange(wmset[start..]));
        bank.Validate();
        return bank;
    }

    public static NativeVibrationBank ReadTable(byte[] bytes, string? sourceHash = null)
    {
        return Read(bytes, sourceHash ?? Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static NativeVibrationBank Read(ReadOnlySpan<byte> bytes, string hash)
    {
        int tableBytes = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (tableBytes <= 0 || tableBytes % 4 != 0 || tableBytes > bytes.Length)
        {
            throw new InvalidDataException("Original vibration bank has no complete relative pointer table.");
        }
        ImmutableArray<int>.Builder offsets = ImmutableArray.CreateBuilder<int>(tableBytes / 4);
        for (int index = 0; index < tableBytes / 4; index++)
        {
            offsets.Add(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(index * 4, 4)));
        }
        NativeVibrationBank bank = new(hash, offsets.MoveToImmutable(), ImmutableArray.CreateRange(bytes.ToArray()));
        bank.Validate();
        return bank;
    }
}
