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
            ImmutableArray<NativeBattleEffectInitializerResource> initializerResources = initializer == 0 ? [] : ReadInitializer(initializer);
            result.Add(new(effect, entry, initializer) { InitializerIsRet = initializer == 0 ? null : FirstByte(initializer) == 0xC3, InitializerResource = initializerResources.Length == 1 ? initializerResources[0] : null, InitializerResources = initializerResources.Length > 1 ? initializerResources : null });
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

        ImmutableArray<NativeBattleEffectInitializerResource> ReadInitializer(uint address)
        {
            ReadOnlySpan<byte> code = Bytes(address, 19);
            if (code[0] != 0x68 || code[5] != 0xE8 || address + 10L + BinaryPrimitives.ReadInt32LittleEndian(code[6..]) != 0x571B80)
            {
                return [];
            }
            if (code[10] == 0x83 && code[11] == 0xC4 && code[12] == 4 && code[13] == 0xA3 && code[18] == 0xC3)
            {
                return [ReadResource(BinaryPrimitives.ReadUInt32LittleEndian(code[1..]), BinaryPrimitives.ReadUInt32LittleEndian(code[14..]))];
            }
            if (code[10] != 0x68 || code[15] != 0xA3)
            {
                return [];
            }
            code = Bytes(address, 34);
            if (code[20] != 0xE8 || address + 25L + BinaryPrimitives.ReadInt32LittleEndian(code[21..]) != 0x571B80 || code[25] != 0x83 || code[26] != 0xC4 || code[27] != 8 || code[28] != 0xA3 || code[33] != 0xC3)
            {
                return [];
            }
            NativeBattleEffectInitializerResource first = ReadResource(BinaryPrimitives.ReadUInt32LittleEndian(code[1..]), BinaryPrimitives.ReadUInt32LittleEndian(code[16..]));
            NativeBattleEffectInitializerResource second = ReadResource(BinaryPrimitives.ReadUInt32LittleEndian(code[11..]), BinaryPrimitives.ReadUInt32LittleEndian(code[29..]));
            if (first.GlobalAddress == second.GlobalAddress)
            {
                throw new InvalidDataException("Native two-load initializer aliases its publication globals.");
            }
            return [first, second];
        }

        NativeBattleEffectInitializerResource ReadResource(uint nameAddress, uint global)
        {
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
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
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
            if (binding.Effect == 142)
            {
                binding = binding with { FiraResources = ReadNativeFiraResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 143)
            {
                binding = binding with { FiragaResources = ReadNativeFiragaResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 102)
            {
                binding = binding with { ThundaraResources = ReadNativeThundaraResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 103)
            {
                binding = binding with { BlizzaraResources = ReadNativeBlizzaraResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 104)
            {
                binding = binding with { BlizzagaResources = ReadNativeBlizzagaResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 118)
            {
                binding = binding with { AeroResources = ReadNativeAeroResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 91)
            {
                binding = binding with { Rush91Resources = ReadNativeRush91Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 92)
            {
                binding = binding with { SearchResources = ReadNativeSearchResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 93)
            {
                binding = binding with { Recover93Resources = ReadNativeRecover93Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 94)
            {
                binding = binding with { Reverse94Resources = ReadNativeReverse94Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 96)
            {
                binding = binding with { Moogle96Resources = ReadNativeMoogle96Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 97)
            {
                if (binding.Entry != 0x729A60 || binding.Initializer != 0x729A40)
                {
                    throw new InvalidDataException("Native Choco differs from its original module identity.");
                }
                binding = binding with { Choco97Resources = ReadNativeChoco97Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 98)
            {
                if (binding.Entry != 0x721860 || binding.Initializer != 0x721840)
                {
                    throw new InvalidDataException("Native Choco98 differs from its original module identity.");
                }
                binding = binding with { Choco98Resources = ReadNativeChoco98Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 99)
            {
                if (binding.Entry != 0x717D30 || binding.Initializer != 0x717D10)
                {
                    throw new InvalidDataException("Native Choco99 differs from its original module identity.");
                }
                binding = binding with { Choco99Resources = ReadNativeChoco99Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 100)
            {
                if (binding.Entry != 0x70D390 || binding.Initializer != 0x70D370)
                {
                    throw new InvalidDataException("Native Choco100 differs from its original module identity.");
                }
                binding = binding with { Choco100Resources = ReadNativeChoco100Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 222)
            {
                if (binding.Entry != 0xA9B200 || binding.Initializer != 0xA9B1D0)
                {
                    throw new InvalidDataException("Native Water differs from its original module identity.");
                }
                binding = binding with { Water222Resources = ReadNativeWater222Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 199)
            {
                if (binding.Entry != 0x5A8750 || binding.Initializer != 0x5A8730 || binding.InitializerIsRet != false || binding.InitializerResource is not null || binding.InitializerResources is not null || binding.InitializerAsset is not null || binding.InitializerAssets is not null)
                {
                    throw new InvalidDataException("Native Cactuar199 differs from its actual heap-resource initializer contract.");
                }
                catalog = catalog.SetItem(index, binding with { Cactuar199Resources = ReadNativeCactuar199Resources() });
                continue;
            }
            if (binding.Effect == 175)
            {
                if (binding.Entry != 0x5D1AD0 || binding.Initializer != 0x5D1AB0)
                {
                    throw new InvalidDataException("Native Holy differs from its original entry and initializer identity.");
                }
                binding = binding with { Holy175Resources = ReadNativeHoly175Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 105)
            {
                if (binding.Entry != 0x6D3DB0 || binding.Initializer != 0x6D3DC0)
                {
                    throw new InvalidDataException("Native Thundaga105 differs from its original entry and initializer identity.");
                }
                binding = binding with { Thundaga105Resources = ReadNativeThundaga105Resources() };
                catalog = catalog.SetItem(index, binding);
            }
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
            if (binding.Entry == 0x6652C0)
            {
                binding = binding with { Effect311Resources = ReadNativeEffect311Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x6D1310)
            {
                binding = binding with { Effect107Resources = ReadNativeEffect107Resources() };
                catalog = catalog.SetItem(index, binding);
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
            if (binding.Entry is 0x8649C0 or 0x8D4450)
            {
                binding = binding with { LifeResources = ReadNativeLifeResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x8D5080)
            {
                binding = binding with { DoubleResources = ReadNativeDoubleResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x89DEF0)
            {
                binding = binding with { TripleResources = ReadNativeTripleResources() };
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
            if (binding.Entry is 0x88CF70 or 0x883110 or 0x7FD150 or 0x7DEA20)
            {
                binding = binding with { RestorationResources = ReadNativeRestorationResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry is 0x811F70 or 0x7F7CB0 or 0x7F2810)
            {
                binding = binding with { RecoveryResources = ReadNativeRecoveryResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Initializer == 0 || binding.InitializerIsRet == true)
            {
                continue;
            }
            ImmutableArray<NativeBattleEffectInitializerResource> resources = NativeBattleEffectInitializerPolicy.Resources(binding);
            if (resources.IsEmpty)
            {
                throw new NotSupportedException($"Native effect{binding.Effect} initializer{binding.Initializer:X} has no proved straight-line571B80 resource loader.");
            }
            ImmutableArray<NativeBattleEffectInitializerAsset> supplied = NativeBattleEffectInitializerPolicy.Assets(binding);
            if (!supplied.IsEmpty)
            {
                continue;
            }
            ImmutableArray<NativeBattleEffectInitializerAsset> assets = resources.Select(resource => new NativeBattleEffectInitializerAsset(resource.Name, Read(magic, resource.Name).ToImmutableArray())).ToImmutableArray();
            catalog = catalog.SetItem(index, binding.InitializerResources is null ? binding with { InitializerAsset = assets[0] } : binding with { InitializerAssets = assets });
        }
        return catalog;
    }
}
