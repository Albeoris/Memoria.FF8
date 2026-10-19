using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleTonberry90ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(2);
        for (short id = 416; id <= 417; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * (uint)id), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Tonberry file directory requires its original terminated name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleTonberry90Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 2)
        {
            throw new InvalidDataException("Native Tonberry requires both original archive files.");
        }
        for (int index = 0; index < files.Length; index++)
        {
            if (files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native Tonberry requires the complete original archive file selected by its directory.");
            }
        }
        ImmutableArray<byte> vibrationBytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0x16D7CD0, 0x11C);
        if (BinaryPrimitives.ReadUInt32LittleEndian(vibrationBytes.AsSpan()) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(vibrationBytes.AsSpan().Slice(4)) != 0x100)
        {
            throw new InvalidDataException("Native Tonberry vibration requires its original null and program1 directory entries.");
        }
        NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [0, 0x100], vibrationBytes);
        NativeBattleTonberry90Resources resources = new(0x1547170, new("FF8_EN:1547170", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1547170, 0x7B00)), new(0x16D7CD0, vibration), files);
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleTonberry90Resources? nativeTonberry90Resources;

    public NativeBattleTonberry90Resources ReadNativeTonberry90Resources()
    {
        if (nativeTonberry90Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleTonberry90ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            using IFlArchive magic = OpenArchive("magic");
            ImmutableArray<NativeBattleEffectInitializerAsset> tims = new[] { "mag089_0.tim", "mag089_1.tim", "mag089_2.tim" }.Select(name => new NativeBattleEffectInitializerAsset(name, Read(magic, name).ToImmutableArray())).ToImmutableArray();
            nativeTonberry90Resources = NativeBattleTonberry90ResourcesReader.Read(executable, files) with { InitializerTims = tims };
        }
        return nativeTonberry90Resources;
    }
}
