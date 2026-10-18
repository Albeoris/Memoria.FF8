using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99IoResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(21);
        for (short id = 468; id <= 488; id++)
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

    public static NativeBattleChoco99IoResources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> script = memory.Read(0x152AAA0, 0x1A0).ToImmutableArray();
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 21 || U16(0) != 0 || U16(0x190) != 0xFF)
        {
            throw new InvalidDataException("Native Choco IO requires its complete script and twenty-one original files.");
        }
        int index = 0;
        for (int offset = 0; offset < script.Length; offset += 16)
        {
            ushort opcode = U16(offset);
            if (opcode is not (0 or 1 or 2 or 3 or 4 or 0xFF))
            {
                throw new NotSupportedException("Native Choco IO script selects an unreached operation.");
            }
            if (BinaryPrimitives.ReadUInt32LittleEndian(script.AsSpan().Slice(offset + 12)) != 0)
            {
                throw new InvalidDataException("Native Choco original IO script requires its cold destination fields.");
            }
            if (opcode is 1 or 2 or 3 or 4)
            {
                if (index >= files.Length || files[index].FileId != names[index].FileId || U16(offset + 2) != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefault || files[index].Data.Length != BinaryPrimitives.ReadInt32LittleEndian(script.AsSpan().Slice(offset + 8)))
                {
                    throw new InvalidDataException("Native Choco IO input contradicts its original file identity or declared length.");
                }
                index++;
            }
        }
        if (index != files.Length)
        {
            throw new InvalidDataException("Native Choco IO requires every original file record.");
        }
        return new(new("FF8_EN:152AAA0", script), files);

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(script.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco99IoResources? nativeChoco99IoResources;

    public NativeBattleChoco99IoResources ReadNativeChoco99IoResources()
    {
        if (nativeChoco99IoResources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleChoco99IoResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeChoco99IoResources = NativeBattleChoco99IoResourcesReader.Read(executable, files);
        }
        return nativeChoco99IoResources;
    }
}
