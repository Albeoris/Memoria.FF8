using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeDrawPointCatalogReader
{
    public static NativeDrawPointCatalog Read(byte[] executable, byte[] kernel)
    {
        string executableHash = Convert.ToHexString(SHA256.HashData(executable));
        if (executableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || kernel.Length < 228 || BinaryPrimitives.ReadUInt32LittleEndian(kernel) != 56)
        {
            throw new NotSupportedException("Draw-point catalog requires the authenticated original English executable and kernel.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        ReadOnlySpan<byte> ReadAddress(uint address, int length)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + length <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), length);
        }
        uint commonAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadAddress(0xB92324, 4));
        ReadOnlySpan<byte> common = ReadAddress(commonAddress, 320);
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(common.Slice(28, 4)));
        int end = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(common.Slice(32, 4)));
        if (commonAddress != 0xB921E4 || BinaryPrimitives.ReadUInt32LittleEndian(common) != 36 || start < 36 || end <= start || end > common.Length || common[end - 1] != 0)
        {
            throw new InvalidDataException("Original draw-point common message7 bounds are invalid.");
        }
        string text = FF8Encoding.CreateEuropean().GetString(common.Slice(start, end - start - 1));
        int commandStart = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 12)));
        int commandEnd = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(8 + 4 * 12)));
        if (commandStart < 228 || commandEnd > kernel.Length || commandEnd - commandStart != 19 * 8)
        {
            throw new InvalidDataException("Original command ability section12 requires nineteen8-byte records.");
        }
        ImmutableArray<byte> commands = Enumerable.Range(0, 19).Select(index => kernel[commandStart + index * 8 + 5]).ToImmutableArray();
        NativeDrawPointCatalog catalog = new(executableHash, Convert.ToHexString(SHA256.HashData(kernel)), ReadAddress(0xB92328, 256).ToArray().ToImmutableArray(), commands, text, Convert.ToHexString(SHA256.HashData(common.Slice(start, end - start))));
        catalog.Validate();
        return catalog;
    }
}
