using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco98IoResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(14);
        for (short id = 454; id <= 467; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * (uint)id), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Choco IO file directory requires its original terminated name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleChoco98IoResources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> script = memory.Read(0x152B2A8, 0x120).ToImmutableArray();
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 14 || U16(0) != 0 || U16(0x110) != 0xFF)
        {
            throw new InvalidDataException("Native Choco IO requires its complete original script and fourteen BATTLE file owners.");
        }
        int fileIndex = 0;
        for (int row = 0; row < 18; row++)
        {
            int offset = row * 16;
            ushort opcode = U16(offset);
            if (opcode is not (0 or 1 or 2 or 3 or 0xFF))
            {
                throw new NotSupportedException("Native Choco IO script selects an unreached operation.");
            }
            if (BinaryPrimitives.ReadUInt32LittleEndian(script.AsSpan().Slice(offset + 12)) != 0)
            {
                throw new InvalidDataException("Native Choco IO original script source requires its cold destination fields.");
            }
            if (opcode is 1 or 2 or 3)
            {
                if (fileIndex >= files.Length)
                {
                    throw new InvalidDataException("Native Choco IO script exceeds its supplied file owners.");
                }
                NativeBattleEffectFileAsset file = files[fileIndex];
                int length = BinaryPrimitives.ReadInt32LittleEndian(script.AsSpan().Slice(offset + 8));
                if (U16(offset + 2) != names[fileIndex].FileId || file.FileId != names[fileIndex].FileId || file.Name != names[fileIndex].Name || file.Data.IsDefault || file.Data.Length != length)
                {
                    throw new InvalidDataException("Native Choco IO file identity or declared extent disagrees with its actual original script.");
                }
                fileIndex++;
            }
        }
        if (fileIndex != 14)
        {
            throw new InvalidDataException("Native Choco IO script requires fourteen complete file records.");
        }
        return new(new("FF8_EN:152B2A8", script), files);

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(script.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco98IoResources? nativeChoco98IoResources;
    public NativeBattleChoco98IoResources ReadNativeChoco98IoResources()
    {
        if (nativeChoco98IoResources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleChoco98IoResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeChoco98IoResources = NativeBattleChoco98IoResourcesReader.Read(executable, files);
        }
        return nativeChoco98IoResources;
    }
}
