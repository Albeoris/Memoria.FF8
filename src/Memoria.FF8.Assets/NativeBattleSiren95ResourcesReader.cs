using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSiren95ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(20);
        for (short id = 418; id <= 437; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * (uint)id), 4));
            byte[] name = memory.Read(pointer, 128);
            int length = name.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Siren file directory requires its terminated original name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(name.AsSpan(0, length)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleSiren95Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length)
        {
            throw new InvalidDataException("Native Siren requires all original archive files selected by its IO programme.");
        }
        for (int index = 0; index < names.Length; index++)
        {
            if (files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native Siren archive data differs from its original file directory.");
            }
        }
        NativeExecutableMemory memory = new(executable);
        NativeBattleSiren95Resources result = new(NativeBattleEffectStaticResourcesReader.Read(memory, 0x1533850, 0x180), files);
        ImmutableArray<byte> vibration = NativeBattleEffectStaticResourcesReader.Read(memory, 0x16A3FB0, 0x13A);
        result = result with { Vibration = new(0x16A3FB0, new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [0, 0x100], vibration)), SoundDescriptors = new("FF8_EN:1533014", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1533014, 0x10)), CameraPrograms = new("FF8_EN:1533A98", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1533A98, 0x358)), PhaseOneSource = new(0x16A5380, NativeBattleEffectStaticResourcesReader.Read(memory, 0x16A5380, 0x5A00)) };
        result = result with { PhaseOneSprites = new("FF8_EN:1533024", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1533024, 0x1AC)) };
        result = result with { ParticleResources = new(NativeBattleEffectStaticResourcesReader.Read(memory, 0x1533DF0, 32), new("FF8_EN:15334A0", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15334A0, 0x12C)), new("FF8_EN:15336C4", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15336C4, 0x144)), new("FF8_EN:15331D0", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15331D0, 0x140)), new("FF8_EN:1533328", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1533328, 0x130))) };
        result = result with { NodeSchedule = new("FF8_EN:15339D8", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15339D8, 0xBE)), StaticNodeSource = new("FF8_EN:16A40EC", NativeBattleEffectStaticResourcesReader.Read(memory, 0x16A40EC, 0x1294)) };
        result = result with { PhaseTwoSprites = new("FF8_EN:15335CC", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15335CC, 0xF8)) };
        byte[] overlay = memory.Read(0x1533E10, 0x60);
        result = result with { OverlayProgram = new(0x1533E10, Enumerable.Range(0, 24).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(index * 4))).ToImmutableArray()) };
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSiren95Resources? nativeSiren95Resources;

    public NativeBattleSiren95Resources ReadNativeSiren95Resources()
    {
        if (nativeSiren95Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleSiren95ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeSiren95Resources = NativeBattleSiren95ResourcesReader.Read(executable, files);
        }
        return nativeSiren95Resources;
    }
}
