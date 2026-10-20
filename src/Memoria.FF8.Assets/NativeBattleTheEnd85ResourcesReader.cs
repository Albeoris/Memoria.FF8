using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleTheEnd85ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(402, 3).Select(index =>
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)index * 4), 4));
            byte[] name = memory.Read(pointer, 128);
            int length = name.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native The End requires its terminated original archive file names.");
            }
            return new NativeBattleEffectFileAsset(checked((short)index), Encoding.ASCII.GetString(name.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleTheEnd85Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length)
        {
            throw new InvalidDataException("Native The End requires its three original archive files.");
        }
        ImmutableArray<byte> script = Read(0x1571E70, 80);
        for (int index = 0; index < names.Length; index++)
        {
            int record = (index + 1) * 16;
            if (files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefaultOrEmpty || BinaryPrimitives.ReadUInt16LittleEndian(script.AsSpan().Slice(record)) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(script.AsSpan().Slice(record + 2)) != names[index].FileId || BinaryPrimitives.ReadInt32LittleEndian(script.AsSpan().Slice(record + 8)) != files[index].Data.Length)
            {
                throw new InvalidDataException("Native The End archive input differs from its original IO script.");
            }
        }
        ImmutableArray<byte> overlay = Read(0x1571FE8, 160);
        return new(new("FF8_EN:1571CBC", Read(0x1571CBC, 0x1B4)), script, files, [new(0x156C7F0, Read(0x156C7F0, 0x54CC)), new(0x17151EC, Read(0x17151EC, 0xA0C4))], new(0x1571FE8, Enumerable.Range(0, 40).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan().Slice(index * 4))).ToImmutableArray()), Dword(0x156C7F0), Dword(0xB8B7D8), ImmutableDictionary<uint, uint>.Empty.Add(0x156C7F4, Dword(0x156C7F4)).Add(0x156C7F8, Dword(0x156C7F8)));

        ImmutableArray<byte> Read(uint address, int length) => memory.Read(address, length).ToImmutableArray();
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleTheEnd85Resources? nativeTheEnd85Resources;
    public NativeBattleTheEnd85Resources ReadNativeTheEnd85Resources()
    {
        if (nativeTheEnd85Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleTheEnd85ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeTheEnd85Resources = NativeBattleTheEnd85ResourcesReader.Read(executable, files);
        }
        return nativeTheEnd85Resources;
    }
}
