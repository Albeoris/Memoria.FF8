using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco100IoResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(16);
        for (short id = 489; id <= 504; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * (uint)id), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Choco IO file directory requires its actual terminated name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleChoco100IoResources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> script = memory.Read(0x1529B70, 0x160).ToImmutableArray();
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 16 || U16(0) != 0 || U16(0x150) != 0xFF)
        {
            throw new InvalidDataException("Native Choco IO requires its complete script and sixteen original files.");
        }
        int index = 0;
        for (int offset = 0; offset < script.Length; offset += 16)
        {
            ushort opcode = U16(offset);
            if (opcode is not (0 or 1 or 2 or 3 or 5 or 0xFF))
            {
                throw new NotSupportedException("Native Choco IO script selects an unreached operation.");
            }
            if (BinaryPrimitives.ReadUInt32LittleEndian(script.AsSpan().Slice(offset + 12)) != 0)
            {
                throw new InvalidDataException("Native Choco original IO script requires its cold destination fields.");
            }
            if (opcode is 1 or 2 or 3 or 5)
            {
                if (index >= files.Length)
                {
                    throw new InvalidDataException($"Native Choco IO record{offset / 16} requests file{U16(offset + 2)} beyond the supplied file catalog.");
                }
                int declaredLength = BinaryPrimitives.ReadInt32LittleEndian(script.AsSpan().Slice(offset + 8));
                if (files[index].FileId != names[index].FileId || U16(offset + 2) != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefaultOrEmpty)
                {
                    int actualLength = files[index].Data.IsDefault ? -1 : files[index].Data.Length;
                    throw new InvalidDataException($"Native Choco IO record{offset / 16} requests file{U16(offset + 2)} ({names[index].Name}), declared length{declaredLength}; supplied file{files[index].FileId} ({files[index].Name}), actual length{actualLength}, expected directory ID{names[index].FileId}.");
                }
                index++;
            }
        }
        if (index != files.Length)
        {
            throw new InvalidDataException("Native Choco IO requires every original file record.");
        }
        return new(new("FF8_EN:1529B70", script), files);

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(script.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco100IoResources? nativeChoco100IoResources;

    public NativeBattleChoco100IoResources ReadNativeChoco100IoResources()
    {
        if (nativeChoco100IoResources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleChoco100IoResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeChoco100IoResources = NativeBattleChoco100IoResourcesReader.Read(executable, files);
        }
        return nativeChoco100IoResources;
    }
}
