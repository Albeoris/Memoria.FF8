using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
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
            result.Add(new(effect, entry, initializer) { InitializerIsRet = initializer == 0 ? null : FirstByte(initializer) == 0xC3, InitializerResource = initializer == 0 ? null : ReadInitializer(initializer) });
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

        ReadOnlySpan<byte> Bytes(uint address, int count)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + count <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), count);
        }

        NativeBattleEffectInitializerResource? ReadInitializer(uint address)
        {
            ReadOnlySpan<byte> code = Bytes(address, 19);
            if (code[0] != 0x68 || code[5] != 0xE8 || code[10] != 0x83 || code[11] != 0xC4 || code[12] != 4 || code[13] != 0xA3 || code[18] != 0xC3 || address + 10L + BinaryPrimitives.ReadInt32LittleEndian(code[6..]) != 0x571B80)
            {
                return null;
            }
            uint nameAddress = BinaryPrimitives.ReadUInt32LittleEndian(code[1..]);
            uint global = BinaryPrimitives.ReadUInt32LittleEndian(code[14..]);
            long globalRva = global - (long)reader.PEHeaders.PEHeader!.ImageBase;
            if (!reader.PEHeaders.SectionHeaders.Any(section => (section.SectionCharacteristics & SectionCharacteristics.MemWrite) != 0 && globalRva >= section.VirtualAddress && globalRva + 4 <= section.VirtualAddress + section.VirtualSize))
            {
                throw new InvalidDataException("Native initializer global is outside its writable image storage.");
            }
            int length = 0;
            while (Bytes(checked(nameAddress + (uint)length), 1)[0] != 0)
            {
                length++;
            }
            return new(Encoding.ASCII.GetString(Bytes(nameAddress, length)), global);
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
            byte[] executable = File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe"));
            ImmutableArray<NativeBattleEffectBinding> catalog = NativeBattleEffectCatalogReader.Read(executable);
            NativeBattleEffectBinding fire = catalog[1];
            if (fire.Entry != 0x6298A0 || fire.Initializer != 0x629880)
            {
                throw new InvalidDataException("Native Fire binding differs from its original resource initializer.");
            }
            if (fire.InitializerResource != new NativeBattleEffectInitializerResource("mag001.tim", 0x24CBD68))
            {
                throw new InvalidDataException("Native Fire resource loader differs from its original571B80/global-store contract.");
            }
            using IFlArchive magic = OpenArchive("magic");
            //6298A0 passes DED5FC to5099A0; its next static object is the first Fire sprite at DEDC2C.
            NativeBattleEffectProgramResources program = new(new("FF8_EN:DED5FC", NativeBattleEffectStaticResourcesReader.Read(executable, 0xDED5FC, 0x630)), [NativeBattleEffectStaticResourcesReader.ReadSprite(executable, 0xDEDC2C), NativeBattleEffectStaticResourcesReader.ReadSprite(executable, 0xDEDDD8), NativeBattleEffectStaticResourcesReader.ReadSprite(executable, 0xDEDF84)], BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0xDEE130, 4).AsSpan()));
            nativeEffectCatalog = catalog.SetItem(1, fire with { InitializerAsset = new("mag001.tim", Read(magic, "mag001.tim").ToImmutableArray()), ProgramResources = program });
        }
        return nativeEffectCatalog;
    }

    public ImmutableArray<NativeBattleEffectBinding> ReadNativeEffectCatalogForEffects(IEnumerable<ushort> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ushort[] requested = effects.Distinct().ToArray();
        if (requested.Any(effect => effect is < 1 or > 400))
        {
            throw new ArgumentOutOfRangeException(nameof(effects), "Native effect materialization requires original effect IDs1..400.");
        }
        ImmutableArray<NativeBattleEffectBinding> catalog = ReadNativeEffectCatalog();
        using IFlArchive magic = OpenArchive("magic");
        foreach (ushort requestedEffect in requested)
        {
            int index = catalog[requestedEffect - 1].Entry == 0 ? 0 : requestedEffect - 1;
            NativeBattleEffectBinding binding = catalog[index];
            if (binding.Entry == 0x61ABA0)
            {
                binding = binding with { BlizzardResources = ReadNativeBlizzardResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x6C3550 && binding.Initializer == 0x6C3560)
            {
                catalog = catalog.SetItem(index, binding with { NativeQuezacotlResources = ReadNativeQuezacotlResources() });
                continue;
            }
            if (binding.Entry == 0x700C10)
            {
                binding = binding with { Effect3Resources = ReadNativeEffect3Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x8D6A00)
            {
                binding = binding with { CureResources = ReadNativeCureResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x889D60)
            {
                binding = binding with { CuraResources = ReadNativeCuraResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x87DB10)
            {
                binding = binding with { CuragaResources = ReadNativeCuragaResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry is 0x88CF70 or 0x883110)
            {
                binding = binding with { RestorationResources = ReadNativeRestorationResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Initializer == 0 || binding.InitializerIsRet == true || binding.InitializerAsset is not null)
            {
                continue;
            }
            NativeBattleEffectInitializerResource resource = binding.InitializerResource ?? throw new NotSupportedException($"Native effect{binding.Effect} initializer{binding.Initializer:X} has no proved straight-line571B80 resource loader.");
            catalog = catalog.SetItem(index, binding with { InitializerAsset = new(resource.Name, Read(magic, resource.Name).ToImmutableArray()) });
        }
        return catalog;
    }
}
