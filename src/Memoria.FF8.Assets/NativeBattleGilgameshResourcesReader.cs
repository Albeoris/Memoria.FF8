using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshResourcesReader
{
    public static NativeBattleGilgameshDirectorResources ReadDirector(byte[] executable, ushort effect = 330)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(effect);
        NativeBattleGilgameshDirectorResources result = new(new($"FF8_EN:{profile.CameraAddress:X}", memory.Read(profile.CameraAddress, profile.CameraLength).ToImmutableArray()), new(memory.SourceHash, [0, 0x100], memory.Read(profile.VibrationAddress, profile.VibrationLength).ToImmutableArray()), U32(0xCD0378), U32(0xCD037C), U32(0xCD0380), U32(profile.ClosingSoundAddress)) { Effect = effect };
        result.Validate();
        return result;

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }

    public static ImmutableArray<NativeBattleArchiveFile> ReadFileNames(byte[] executable, ushort effect = 330)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<ushort> ids = NativeBattleGilgameshResources.FileIdsForEffect(effect);
        ImmutableArray<NativeBattleArchiveFile>.Builder files = ImmutableArray.CreateBuilder<NativeBattleArchiveFile>(ids.Length);
        foreach (ushort id in ids)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + id * 4u), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Excalipoor external file name requires its bounded original directory string.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes, 0, end).ToLowerInvariant(), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleGilgameshResources Read(byte[] executable, ImmutableArray<NativeBattleArchiveFile> files, ushort effect = 330)
    {
        ImmutableArray<NativeBattleArchiveFile> names = ReadFileNames(executable, effect);
        NativeBattleGilgameshResources result = new(files) { Effect = effect };
        result.Validate();
        for (int index = 0; index < names.Length; index++)
        {
            if (files[index].FileId != names[index].FileId || !string.Equals(files[index].Name, names[index].Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Native Excalipoor supplied files must match their authenticated directory selections.");
            }
        }
        return result;
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleGilgameshResources> nativeGilgameshResourcesByEffect = [];
    private readonly Dictionary<ushort, NativeBattleGilgameshDirectorResources> nativeGilgameshDirectorResourcesByEffect = [];

    public NativeBattleGilgameshDirectorResources ReadNativeGilgameshDirectorResources(ushort effect = 330)
    {
        if (!nativeGilgameshDirectorResourcesByEffect.TryGetValue(effect, out NativeBattleGilgameshDirectorResources? selected))
        {
            selected = NativeBattleGilgameshResourcesReader.ReadDirector(GameDataResources.ReadNativeExecutable(resources), effect);
            nativeGilgameshDirectorResourcesByEffect.Add(effect, selected);
        }
        return selected;
    }

    public NativeBattleGilgameshResources ReadNativeGilgameshResources(ushort effect = 330)
    {
        if (!nativeGilgameshResourcesByEffect.TryGetValue(effect, out NativeBattleGilgameshResources? selected))
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleArchiveFile> files = NativeBattleGilgameshResourcesReader.ReadFileNames(executable, effect).Select(file => file with { Bytes = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            selected = NativeBattleGilgameshResourcesReader.Read(executable, files, effect);
            nativeGilgameshResourcesByEffect.Add(effect, selected);
        }
        return selected;
    }
}
