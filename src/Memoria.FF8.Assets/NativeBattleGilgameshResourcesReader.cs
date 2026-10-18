using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshResourcesReader
{
    public static NativeBattleGilgameshDirectorResources ReadDirector(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGilgameshDirectorResources result = new(new("FF8_EN:CD0874", memory.Read(0xCD0874, 0xEA).ToImmutableArray()), new(memory.SourceHash, [0, 0x100], memory.Read(0xCD0230, 0x14A).ToImmutableArray()), U32(0xCD0378), U32(0xCD037C), U32(0xCD0380), U32(0xCD0390));
        result.Validate();
        return result;

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }

    public static ImmutableArray<NativeBattleArchiveFile> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleArchiveFile>.Builder files = ImmutableArray.CreateBuilder<NativeBattleArchiveFile>(NativeBattleGilgameshResources.FileIds.Length);
        foreach (ushort id in NativeBattleGilgameshResources.FileIds)
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

    public static NativeBattleGilgameshResources Read(byte[] executable, ImmutableArray<NativeBattleArchiveFile> files)
    {
        ImmutableArray<NativeBattleArchiveFile> names = ReadFileNames(executable);
        NativeBattleGilgameshResources result = new(files);
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
    private NativeBattleGilgameshResources? nativeGilgameshResources;
    private NativeBattleGilgameshDirectorResources? nativeGilgameshDirectorResources;

    public NativeBattleGilgameshDirectorResources ReadNativeGilgameshDirectorResources() => nativeGilgameshDirectorResources ??= NativeBattleGilgameshResourcesReader.ReadDirector(GameDataResources.ReadNativeExecutable(this.resources));

    public NativeBattleGilgameshResources ReadNativeGilgameshResources()
    {
        if (nativeGilgameshResources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleArchiveFile> files = NativeBattleGilgameshResourcesReader.ReadFileNames(executable).Select(file => file with { Bytes = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeGilgameshResources = NativeBattleGilgameshResourcesReader.Read(executable, files);
        }
        return nativeGilgameshResources;
    }
}
