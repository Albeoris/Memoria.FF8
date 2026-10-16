using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGraphicsRulesReader
{
    public static NativeBattleGraphicsRules Read(byte[] executable)
    {
        string hash = Convert.ToHexString(SHA256.HashData(executable));
        if (hash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native graphics status maps require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        long rva = 0xB8BA08 - (long)reader.PEHeaders.PEHeader!.ImageBase;
        SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 48 <= section.VirtualAddress + section.SizeOfRawData);
        int offset = checked((int)(rva - section.VirtualAddress + section.PointerToRawData));
        NativeBattleGraphicsRules rules = new(hash, executable.AsSpan(offset, 16).ToArray().Select(value => unchecked((sbyte)value)).ToImmutableArray(), executable.AsSpan(offset + 16, 32).ToArray().Select(value => unchecked((sbyte)value)).ToImmutableArray());
        rules.Validate();
        return rules;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGraphicsRules? nativeBattleGraphicsRules;

    public NativeBattleGraphicsRules ReadNativeBattleGraphicsRules()
    {
        return nativeBattleGraphicsRules ??= NativeBattleGraphicsRulesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
