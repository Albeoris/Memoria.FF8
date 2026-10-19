using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDoomtrain191ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(7);
        for (short id = 0x22B; id <= 0x231; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCCu + 4u * (uint)id, 4));
            byte[] name = memory.Read(pointer, 128);
            int length = name.AsSpan().IndexOf((byte)0);
            if (length <= 0)
            {
                throw new InvalidDataException("Native Doomtrain requires each original archive directory name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(name.AsSpan(0, length)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleDoomtrain191Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length || files.Where((file, index) => file.FileId != names[index].FileId || file.Name != names[index].Name || file.Data.IsDefaultOrEmpty).Any())
        {
            throw new InvalidDataException("Native Doomtrain requires all original archive files from its timeline's directory.");
        }
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> vibration = NativeBattleEffectStaticResourcesReader.Read(memory, 0xE3C774, 0x136);
        NativeBattleDoomtrain191Resources result = new(Allocation(0xF78384, 0x2D120), Allocation(0xE3CA88, 0x162), new(0xE3C774, new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [0, 0x100], vibration)), Allocation(0xE3C8A8, 20), BinaryPrimitives.ReadInt32LittleEndian(memory.Read(0xF78380, 4)), files);
        if (BinaryPrimitives.ReadUInt16LittleEndian(result.CameraProgram.Data.AsSpan().Slice(0x160)) != 0x8000 || BinaryPrimitives.ReadUInt16LittleEndian(vibration.AsSpan().Slice(0x100)) != 46)
        {
            throw new InvalidDataException("Native Doomtrain requires its original complete camera terminator and vibration program extent.");
        }
        result = result with { OpeningRigs = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xE375BCu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xE38E78u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xE396B0u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xE3B83Cu), new(0, 0xE346B0, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xE346B0))) };
        ImmutableArray<byte> skyPalette = NativeBattleEffectStaticResourcesReader.Read(memory, 0xE3C8DC, 128);
        result = result with { Sky = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xE3496Cu), Allocation(0xE3702C, 0x590), Enumerable.Range(0, 32).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(skyPalette.AsSpan().Slice(index * 4))).ToImmutableArray()) };
        result.Validate();
        ImmutableArray<byte> rainColors = NativeBattleEffectStaticResourcesReader.Read(memory, 0xE3C988, 256);
        result = result with { Sky = result.Sky! with { RainColors = Enumerable.Range(0, 64).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(rainColors.AsSpan().Slice(index * 4))).ToImmutableArray() } };
        return result;

        NativeBattleEffectHeapAllocation Allocation(uint address, int length) => new($"FF8_EN:{address:X}", NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDoomtrain191Resources? nativeDoomtrain191Resources;

    public NativeBattleDoomtrain191Resources ReadNativeDoomtrain191Resources()
    {
        if (nativeDoomtrain191Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleDoomtrain191ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeDoomtrain191Resources = NativeBattleDoomtrain191ResourcesReader.Read(executable, files);
        }
        return nativeDoomtrain191Resources;
    }
}
