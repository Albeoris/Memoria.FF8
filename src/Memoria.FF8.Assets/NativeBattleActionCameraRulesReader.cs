using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleActionCameraRulesReader
{
    public static NativeBattleActionCameraRules Read(byte[] executable)
    {
        string hash = Convert.ToHexString(SHA256.HashData(executable));
        if (hash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native action camera rules require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        NativeBattleActionCameraRules result = new(hash, Enumerable.Range(0, 5).Select(index => Word(0xB8B810u + 4u * (uint)index)).ToImmutableArray(), Enumerable.Range(0, 5).Select(index => Word(0xB8B812u + 4u * (uint)index)).ToImmutableArray(), Enumerable.Range(0, 5).Select(index => Word(0xB8B824u + 2u * (uint)index)).ToImmutableArray());
        result.Validate();
        return result;

        ushort Word(uint address)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 2 <= section.VirtualAddress + section.SizeOfRawData);
            return BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), 2));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleActionCameraRules? nativeActionCameraRules;

    public NativeBattleActionCameraRules ReadNativeActionCameraRules() => nativeActionCameraRules ??= NativeBattleActionCameraRulesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
