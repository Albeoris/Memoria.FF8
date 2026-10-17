using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffectCatalogReader
{
    public static ImmutableArray<NativeBattleEffectBinding> Read(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native effect entry bindings require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        ImmutableArray<NativeBattleEffectBinding>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectBinding>(400);
        for (ushort effect = 1; effect <= 400; effect++)
        {
            uint entry = ReadAddress(0xC81774 + 4u * (uint)(effect - 1));
            uint initializer = ReadAddress(0xC81DB8 + 4u * (uint)(effect - 1));
            ValidateCallback(entry);
            ValidateCallback(initializer);
            result.Add(new(effect, entry, initializer) { InitializerIsRet = initializer == 0 ? null : FirstByte(initializer) == 0xC3 });
        }
        return result.MoveToImmutable();

        uint ReadAddress(uint address)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 4 <= section.VirtualAddress + section.SizeOfRawData);
            return BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), 4));
        }

        byte FirstByte(uint address)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva < section.VirtualAddress + section.SizeOfRawData);
            return executable[checked((int)(rva - section.VirtualAddress + section.PointerToRawData))];
        }

        void ValidateCallback(uint address)
        {
            if (address == 0)
            {
                return;
            }
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            if (!reader.PEHeaders.SectionHeaders.Any(section => (section.SectionCharacteristics & SectionCharacteristics.MemExecute) != 0 && rva >= section.VirtualAddress && rva < section.VirtualAddress + section.SizeOfRawData))
            {
                throw new InvalidDataException($"Native effect callback{address:X} is outside executable image code.");
            }
        }
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<NativeBattleEffectBinding> nativeEffectCatalog;

    public ImmutableArray<NativeBattleEffectBinding> ReadNativeEffectCatalog()
    {
        if (nativeEffectCatalog.IsDefault)
        {
            ImmutableArray<NativeBattleEffectBinding> catalog = NativeBattleEffectCatalogReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
            NativeBattleEffectBinding fire = catalog[1];
            if (fire.Entry != 0x6298A0 || fire.Initializer != 0x629880)
            {
                throw new InvalidDataException("Native Fire binding differs from its original resource initializer.");
            }
            using IFlArchive magic = OpenArchive("magic");
            nativeEffectCatalog = catalog.SetItem(1, fire with { InitializerAsset = new("mag001.tim", Read(magic, "mag001.tim").ToImmutableArray()) });
        }
        return nativeEffectCatalog;
    }
}
