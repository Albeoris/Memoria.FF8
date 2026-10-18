using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWaterIoResourcesReader
{
    public static ImmutableArray<NativeBattleArchiveFile> ReadFileNames(byte[] executable)
    {
        NativeBattleWater222ResourcesReader.Binding(executable);
        ImmutableArray<NativeBattleArchiveFile>.Builder files = ImmutableArray.CreateBuilder<NativeBattleArchiveFile>(3);
        for (ushort id = 363; id <= 365; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(0xB84CCCu + id * 4u), 4).AsSpan());
            byte[] bytes = NativeBattleEffectStaticResourcesReader.Read(executable, pointer, 128).ToArray();
            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Water external file name requires its actual bounded directory string.");
            }
            string name = Encoding.ASCII.GetString(bytes, 0, length).ToLowerInvariant();
            if (name != $"ma8def_p.{id - 363}")
            {
                throw new InvalidDataException("Native Water external file directory contradicts its actual three-file profile.");
            }
            files.Add(new(id, name, []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleWaterIoResources Read(byte[] executable, ImmutableArray<NativeBattleArchiveFile> files)
    {
        ImmutableArray<NativeBattleArchiveFile> names = ReadFileNames(executable);
        NativeBattleWaterIoResources result = new(files);
        result.Validate();
        for (int index = 0; index < names.Length; index++)
        {
            if (files[index].FileId != names[index].FileId || !string.Equals(files[index].Name, names[index].Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Native Water supplied files must match their authenticated directory selections.");
            }
        }
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleWaterIoResources? nativeWaterIoResources;

    public NativeBattleWaterIoResources ReadNativeWaterIoResources()
    {
        if (nativeWaterIoResources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleArchiveFile> files = NativeBattleWaterIoResourcesReader.ReadFileNames(executable).Select(file => file with { Bytes = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeWaterIoResources = NativeBattleWaterIoResourcesReader.Read(executable, files);
        }
        return nativeWaterIoResources;
    }
}
