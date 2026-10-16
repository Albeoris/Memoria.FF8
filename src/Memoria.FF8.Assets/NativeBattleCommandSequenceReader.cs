using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCommandSequenceReader
{
    public static ImmutableArray<byte> Read(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native battle command sequences require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        const int count = 0x27;
        //48D200 indexes B8150C only for physical actors0..2 with commandBYTE<27h; packet+2 owns the selected BYTE.
        long rva = 0xB8150C - (long)reader.PEHeaders.PEHeader!.ImageBase;
        SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + count <= section.VirtualAddress + section.SizeOfRawData);
        int offset = checked((int)(rva - section.VirtualAddress + section.PointerToRawData));
        return executable.AsSpan(offset, count).ToArray().ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<byte> nativeBattleCommandSequences;

    public ImmutableArray<byte> ReadNativeBattleCommandSequences()
    {
        if (nativeBattleCommandSequences.IsDefault)
        {
            nativeBattleCommandSequences = NativeBattleCommandSequenceReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
        }
        return nativeBattleCommandSequences;
    }
}
