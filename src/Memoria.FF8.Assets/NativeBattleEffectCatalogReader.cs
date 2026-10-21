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
            if (address == 0x6897F0)
            {
                ReadOnlySpan<byte> special = Bytes(address, 29);
                if (special[0] != 0x68 || special[5] != 0xC7 || special[6] != 5 || BinaryPrimitives.ReadUInt32LittleEndian(special[7..]) != 0x1DCD6E8 || BinaryPrimitives.ReadUInt32LittleEndian(special[11..]) != 0x689810 || special[15] != 0xE8 || address + 20L + BinaryPrimitives.ReadInt32LittleEndian(special[16..]) != 0x571B80 || special[20] != 0x83 || special[21] != 0xC4 || special[22] != 4 || special[23] != 0xA3 || BinaryPrimitives.ReadUInt32LittleEndian(special[24..]) != 0x2508410 || special[28] != 0xC3)
                {
                    throw new InvalidDataException("Native Duel opening initializer differs from its original callback and texture publication.");
                }
                return [ReadResource(BinaryPrimitives.ReadUInt32LittleEndian(special[1..]), 0x2508410)];
            }
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
            if (binding.Effect == 32)
            {
                binding = binding with { ProtectResources = ReadNativeProtectResources() };
            }
            if (binding.Effect == 33)
            {
                binding = binding with { ShellResources = ReadNativeShellResources() };
            }
            if (binding.Effect == 226)
            {
                binding = binding with { WhiteWind226Resources = ReadNativeWhiteWind226Resources() };
            }
            if (binding.Effect == 26)
            {
                binding = binding with { Clash26Resources = ReadNativeClash26Resources() };
            }
            if (binding.Effect == 145)
            {
                binding = binding with { Sleep145Resources = ReadNativeSleep145Resources() };
            }
            if (binding.Effect == 14)
            {
                binding = binding with { DeathResources = ReadNativeDeathResources() };
            }
            if (binding.Effect == 152)
            {
                binding = binding with { Ultrasonic152Resources = ReadNativeUltrasonic152Resources() };
            }
            if (binding.Effect == 180)
            {
                binding = binding with { Suicide180Resources = ReadNativeSuicide180Resources() };
            }
            if (binding.Effect == 108)
            {
                binding = binding with { Berserk108Resources = ReadNativeBerserk108Resources() };
            }
            if (binding.Effect == 123)
            {
                binding = binding with { Slow123Resources = ReadNativeSlow123Resources() };
            }
            if (binding.Effect == 125)
            {
                binding = binding with { Haste125Resources = ReadNativeHaste125Resources() };
            }
            if (binding.Effect == 115)
            {
                binding = binding with { Float115Resources = ReadNativeFloat115Resources() };
            }
            if (binding.Effect == 121)
            {
                binding = binding with { BlindResources = ReadNativeBlindResources() };
            }
            if (binding.Effect == 147)
            {
                binding = binding with { Regen147Resources = ReadNativeRegen147Resources() };
            }
            if (binding.Effect == 280)
            {
                binding = binding with { FullCure280Resources = ReadNativeFullCure280Resources() };
            }
            if (binding.Effect == 124)
            {
                binding = binding with { Flare124Resources = ReadNativeFlare124Resources() };
            }
            if (binding.Effect == 114)
            {
                binding = binding with { ZombieResources = ReadNativeZombieResources() };
            }
            if (binding.Effect == 119)
            {
                binding = binding with { Stop119Resources = ReadNativeStop119Resources() };
            }
            if (binding.Effect == 288)
            {
                binding = binding with { Rapture288Resources = ReadNativeRapture288Resources() };
            }
            if (binding.Effect == 117)
            {
                binding = binding with { Break117Resources = ReadNativeBreak117Resources() };
            }
            if (binding.Effect == 149)
            {
                binding = binding with { Ultima149Resources = ReadNativeUltima149Resources() };
            }
            if (binding.Effect == 189)
            {
                binding = binding with { Wall189Resources = ReadNativeWall189Resources() };
            }
            if (binding.Effect == 38)
            {
                binding = binding with { Quake38Resources = ReadNativeQuake38Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 85)
            {
                binding = binding with { TheEnd85Resources = ReadNativeTheEnd85Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 34)
            {
                binding = binding with { Pain34Resources = ReadNativePain34Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 109)
            {
                binding = binding with { Dispel109Resources = ReadNativeDispel109Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 146)
            {
                binding = binding with { Tornado146Resources = ReadNativeTornado146Resources() };
            }
            if (binding.Effect == 39)
            {
                binding = binding with { Drain39Resources = ReadNativeDrain39Resources() };
            }
            if (binding.Effect == 41)
            {
                binding = binding with { Dribble41Resources = ReadNativeDribble41Resources() };
            }
            if (binding.Effect == 42)
            {
                binding = binding with { Shoot42Resources = ReadNativeShoot42Resources() };
            }
            if (binding.Effect == 36)
            {
                binding = binding with { Confuse36Resources = ReadNativeConfuse36Resources() };
            }
            if (binding.Effect == 110)
            {
                binding = binding with { Control110Resources = ReadNativeControl110Resources() };
            }
            if (binding.Effect == 17)
            {
                binding = binding with { Landing17Resources = ReadNativeLanding17Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 179)
            {
                binding = binding with { BombGlow179Resources = ReadNativeBombGlow179Resources() };
            }
            if (binding.Effect == 276)
            {
                binding = binding with { XatmDeath276Resources = ReadNativeXatmDeath276Resources() };
            }
            if (binding.Effect == 10)
            {
                binding = binding with { RayBomb10Resources = ReadNativeRayBomb10Resources() };
            }
            if (binding.Effect == 277)
            {
                binding = binding with { FunguarLaser277Resources = ReadNativeFunguarLaser277Resources() };
            }
            if (binding.Effect == 176)
            {
                binding = binding with { SandStorm176Resources = ReadNativeSandStorm176Resources() };
            }
            if (binding.Effect == 154)
            {
                binding = binding with { UltraWaves154Resources = ReadNativeUltraWaves154Resources() };
            }
            if (binding.Effect == 153)
            {
                binding = binding with { StickyWeb153Resources = ReadNativeStickyWeb153Resources() };
            }
            if (binding.Effect == 13)
            {
                binding = binding with { DarkMist13Resources = ReadNativeDarkMist13Resources() };
            }
            if (binding.Effect == 113)
            {
                binding = binding with { BadBreath113Resources = ReadNativeBadBreath113Resources() };
            }
            if (binding.Effect == 148)
            {
                binding = binding with { MeltdownResources = ReadNativeMeltdownResources() };
            }
            if (binding.Effect == 111)
            {
                binding = binding with { AuraResources = ReadNativeAuraResources() };
            }
            if (binding.Effect == 168)
            {
                binding = binding with { SleepingGas168Resources = ReadNativeSleepingGas168Resources() };
            }
            if (binding.Effect == 169)
            {
                binding = binding with { GastricJuice169Resources = ReadNativeGastricJuice169Resources() };
            }
            if (binding.Effect == 106)
            {
                binding = binding with { ReflectResources = ReadNativeReflectResources() };
            }
            if (binding.Effect == 11)
            {
                binding = binding with { Storm11Resources = ReadNativeStorm11Resources() };
            }
            if (binding.Effect is 120 or 122)
            {
                binding = binding with { StatusMagicResources = ReadNativeStatusMagicResources(binding.Effect) };
            }
            if (binding.Effect == 259)
            {
                binding = binding with { Renzokuken259Resources = ReadNativeRenzokuken259Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 332)
            {
                binding = binding with { Renzokuken332Resources = ReadNativeRenzokuken332Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 333)
            {
                binding = binding with { Renzokuken333Resources = ReadNativeRenzokuken333Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 141)
            {
                binding = binding with { Renzokuken141Resources = ReadNativeRenzokuken141Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 160)
            {
                binding = binding with { Renzokuken160Resources = ReadNativeRenzokuken160Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 162)
            {
                binding = binding with { FatedCircle162Resources = ReadNativeFatedCircle162Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 193)
            {
                binding = binding with { Shot193Resources = ReadNativeShot193Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 112)
            {
                if (binding.Entry != 0x6CBBE0 || binding.Initializer != 0x6CBBF0 || binding.InitializerResource != new NativeBattleEffectInitializerResource("mag111.tim", 0x25218E4))
                {
                    throw new InvalidDataException("Native Duel112 differs from its original entry and texture publication.");
                }
                binding = binding with { Duel112Resources = ReadNativeDuel112Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 274)
            {
                if (binding.Entry != 0x6897E0 || binding.Initializer != 0x6897F0 || binding.InitializerResource != new NativeBattleEffectInitializerResource("mag273.tim", 0x2508410))
                {
                    throw new InvalidDataException("Native Duel opening differs from its original entry, initializer and texture publication.");
                }
                binding = binding with { Duel274Resources = ReadNativeDuel274Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 212)
            {
                if (binding.Entry != 0x6A0D20 || binding.Initializer != 0x6A0D30 || binding.InitializerResource != new NativeBattleEffectInitializerResource("mag211.tim", 0x25101F8))
                {
                    throw new InvalidDataException("Native Burning Rave differs from its original entry, initializer and texture publication.");
                }
                binding = binding with { Duel212Resources = ReadNativeDuel212Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 216)
            {
                if (binding.Entry != 0x6959E0 || binding.Initializer != 0x6959F0 || binding.InitializerResource != new NativeBattleEffectInitializerResource("mag215.tim", 0x2508870))
                {
                    throw new InvalidDataException("Native Different Beat differs from its original entry, initializer and texture publication.");
                }
                binding = binding with { Duel216Resources = ReadNativeDuel216Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 215)
            {
                if (binding.Entry != 0x69AB60 || binding.Initializer != 0x69AB70 || binding.InitializerResource != new NativeBattleEffectInitializerResource("mag214.tim", 0x2508938))
                {
                    throw new InvalidDataException("Native Duel215 differs from its original entry, initializer and texture publication.");
                }
                binding = binding with { Duel215Resources = ReadNativeDuel215Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 200)
            {
                if (binding.Entry != 0x6391D0 || binding.Initializer != 0x575530 || binding.InitializerIsRet != true)
                {
                    throw new InvalidDataException("Native No Mercy differs from its original entry and empty initializer.");
                }
                binding = binding with { NoMercy200Resources = ReadNativeNoMercy200Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 165)
            {
                binding = binding with { LionHeart165Resources = ReadNativeLionHeart165Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 164)
            {
                binding = binding with { BlastingZone164Resources = ReadNativeBlastingZone164Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 163)
            {
                binding = binding with { RoughDivide163Resources = ReadNativeRoughDivide163Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 84)
            {
                binding = binding with { AngelWing84Resources = ReadNativeAngelWing84Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 86)
            {
                binding = binding with { Rinoa86Resources = ReadNativeRinoa86Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 87)
            {
                binding = binding with { Rinoa87Resources = ReadNativeRinoa87Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 88)
            {
                binding = binding with { Rinoa88Resources = ReadNativeRinoa88Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 198)
            {
                binding = binding with { Shot198Resources = ReadNativeShot198Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 89)
            {
                if (binding.Entry != 0x6F9CF0 || binding.Initializer != 0x5755E0 || binding.InitializerIsRet != true || binding.InitializerResource is not null || binding.InitializerResources is not null)
                {
                    throw new InvalidDataException("Native Wishing Star differs from its original entry and empty initializer.");
                }
                binding = binding with { Rinoa89Resources = ReadNativeRinoa89Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 197)
            {
                binding = binding with { Shot197Resources = ReadNativeShot197Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 195)
            {
                binding = binding with { Shot195Resources = ReadNativeShot195Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 194)
            {
                binding = binding with { Shot194Resources = ReadNativeShot194Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 192)
            {
                binding = binding with { Shot192Resources = ReadNativeShot192Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 196)
            {
                binding = binding with { Shot196Resources = ReadNativeShot196Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 188)
            {
                binding = binding with { Shot188Resources = ReadNativeShot188Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 40)
            {
                //84D410 returns a BYTE category; retain its actual indexed source row for every possible result.
                binding = binding with { ScanTextResources = ReadNativeScanTextResources(), ScanVisualResources = ReadNativeScanVisualResources(), ScanCameraProfiles = Enumerable.Range(0, 256).Select(category => ReadNativeScanCameraProfile((ushort)category)).ToImmutableArray() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 185)
            {
                binding = binding with { ShivaResources = ReadNativeShivaResources() };
                catalog = catalog.SetItem(index, binding);
            }
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
            if (binding.Effect == 278)
            {
                if (binding.Entry != 0x680C50 || binding.Initializer != 0x680C60)
                {
                    throw new InvalidDataException("Native Carbuncle differs from its original module identity.");
                }
                binding = binding with { Carbuncle278Resources = ReadNativeCarbuncle278Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 325)
            {
                if (binding.Entry != 0x6541E0 || binding.Initializer != 0x6541F0)
                {
                    throw new InvalidDataException("Native Diablos differs from its original module identity.");
                }
                binding = binding with { Diablos325Resources = ReadNativeDiablos325Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 291)
            {
                if (binding.Entry != 0x6ED250)
                {
                    throw new InvalidDataException("Native Pandemona differs from its original module identity.");
                }
                binding = binding with { Pandemona291Resources = ReadNativePandemona291Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 191)
            {
                if (binding.Entry != 0x63E730 || binding.Initializer != 0x575560 || binding.InitializerIsRet != true)
                {
                    throw new InvalidDataException("Native Doomtrain differs from its original module identity.");
                }
                binding = binding with { Doomtrain191Resources = ReadNativeDoomtrain191Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 95)
            {
                if (binding.Entry != 0x739DA0 || binding.Initializer != 0x739D80)
                {
                    throw new InvalidDataException("Native Siren differs from its original module identity.");
                }
                binding = binding with { Siren95Resources = ReadNativeSiren95Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect == 90)
            {
                if (binding.Entry != 0x762360 || binding.Initializer != 0x762320)
                {
                    throw new InvalidDataException("Native Tonberry differs from its original module identity.");
                }
                binding = binding with { Tonberry90Resources = ReadNativeTonberry90Resources() };
                catalog = catalog.SetItem(index, binding);
                continue;
            }
            if (NativeBattleCinematicProfile.IsSupported(binding.Effect))
            {
                binding = binding with { ScriptedInitializationDirectories = ReadNativeCinematicInitializationDirectories(binding.Effect), CinematicResources = ReadNativeCinematicResources(binding.Effect) };
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
            if (binding.Effect is 329 or 330)
            {
                NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(binding.Effect);
                if (binding.Entry != profile.Entry || binding.Initializer != profile.Initializer)
                {
                    throw new InvalidDataException("Native Excalipoor330 differs from its original module identity.");
                }
                binding = binding with { GilgameshResources = ReadNativeGilgameshEffectResources(binding.Effect) };
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
            if (binding.Entry == 0x81FA10)
            {
                binding = binding with { CureResources = ReadNativePotion50Resources() };
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
            if (binding.Entry == 0x89A6B0)
            {
                binding = binding with { Bio22Resources = ReadNativeBio22Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x81C800)
            {
                binding = binding with { CuraResources = ReadNativePotion51Resources() };
            }
            if (binding.Entry == 0x889D60)
            {
                binding = binding with { CuraResources = ReadNativeCuraResources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry is 0x8AB140 or 0x7E58C0 or 0x7A51B0 or 0x79EE30)
            {
                binding = binding with { ActorDirectorResources = ReadNativeActorDirectorResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == NativeBattleDrinkMagic37Profile.Entry)
            {
                binding = binding with { DrinkMagic37Resources = ReadNativeDrinkMagic37Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == NativeBattleRaldo19Profile.Entry)
            {
                binding = binding with { Raldo19Resources = ReadNativeRaldo19Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == NativeBattleEffect20Profile.Entry)
            {
                binding = binding with { Effect20Resources = ReadNativeEffect20Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == NativeBattleWindBlast29Profile.Entry)
            {
                binding = binding with { WindBlast29Resources = ReadNativeWindBlast29Resources() };
            }

            if (binding.Entry == NativeBattleEffect60Profile.Entry)
            {
                binding = binding with { Effect60Resources = ReadNativeEffect60Resources() };
            }

            if (binding.Entry == NativeBattlePsychoBlast23Profile.Entry)
            {
                binding = binding with { PsychoBlast23Resources = ReadNativePsychoBlast23Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry is 0x7BE2D0 or 0x7CBDB0)
            {
                binding = binding with { MightyGuardResources = ReadNativeMightyGuardResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry is 0x666DD0 or 0x6664A0)
            {
                binding = binding with { HeroResources = ReadNativeHeroResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == NativeBattleDrawStock15Profile.Entry)
            {
                binding = binding with { DrawStock15Resources = ReadNativeDrawStock15Resources() };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry is 0x808820 or 0x7D1E80)
            {
                binding = binding with { AquaResources = ReadNativeAquaResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Entry == 0x817410)
            {
                binding = binding with { CuragaResources = ReadNativePotion52Resources() };
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
            if (binding.Entry is 0x8CE3D0 or 0x811F70 or 0x7F7CB0 or 0x7F2810)
            {
                binding = binding with { RecoveryResources = ReadNativeRecoveryResources(binding.Effect) };
                catalog = catalog.SetItem(index, binding);
            }
            if (binding.Effect is >= 292 and <= 296)
            {
                binding = binding with { StatusCureItemResources = ReadNativeStatusCureItemResources(binding.Effect) };
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
