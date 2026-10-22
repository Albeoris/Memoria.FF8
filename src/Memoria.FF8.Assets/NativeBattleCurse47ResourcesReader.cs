using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCurse47ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        return NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 388, 389, 390 });
    }

    public static NativeBattleCurse47Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x15B8290, 11)) != "mag046.tim\0")
        {
            throw new InvalidDataException("Curse requires its original initializer texture name.");
        }
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length || files.Where((file, index) => file is null || file.FileId != names[index].FileId || !string.Equals(file.Name, names[index].Name, StringComparison.OrdinalIgnoreCase)).Any())
        {
            throw new InvalidDataException("Curse archive resources disagree with the executable file directory.");
        }
        uint[] spriteAddresses = [0x17B98A8, 0x17B9A3C, 0x17B9BD0];
        uint[] actAddresses = [0x17BB134, 0x17BDF6C, 0x17C0DA4];
        int[] actLengths = [11832, 11832, 16380];
        NativeBattleCurse47Resources result = new(mainTim,
            new("FF8_EN:15B8030", memory.Read(0x15B8030, 512).ToImmutableArray()), files,
            new("FF8_EN:15B8230", memory.Read(0x15B8230, 80).ToImmutableArray()), ReadDword(0x15B802C), ReadDword(0x15B8028),
            new(memory.Read(0x17B97A4, 6544).ToImmutableArray()) { ResourceAddress = 0x17B97A4 },
            spriteAddresses.Select((address, index) => new NativeBattleModuleResource((byte)index, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray(),
            actAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, memory.Read(address, actLengths[index]).ToImmutableArray())).ToImmutableArray(),
            new(0x15B8280, Enumerable.Range(0, 4).Select(index => ReadDword(0x15B8280u + (uint)index * 4)).ToImmutableArray()));
        result.Validate();
        ValidateTim(mainTim.Data, 9, 320, 240, 256, 9, 384, 256, 320, 256);
        ValidateTim(files[0].Data, 9, 320, 249, 256, 1, 704, 256, 64, 256);
        ValidateTim(files[1].Data, 9, 320, 249, 256, 1, 768, 256, 64, 256);
        ValidateTim(files[2].Data, 8, 320, 250, 16, 1, 832, 256, 64, 256);
        foreach (NativeBattleModuleResource sprite in result.Sprites)
        {
            int count = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
            for (int frame = 0; frame < count; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        return result;

        uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }

    private static void ValidateTim(ImmutableArray<byte> data, uint flags, ushort cx, ushort cy, ushort cw, ushort ch, ushort ix, ushort iy, ushort iw, ushort ih)
    {
        if (data.IsDefault || data.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(4)) != flags)
        {
            throw new InvalidDataException("Curse requires its original indexed texture format.");
        }
        int cursor = 8;
        Block(cx, cy, cw, ch);
        Block(ix, iy, iw, ih);
        if (cursor != data.Length || WorldAssetReader.ReadTim(data.ToArray(), 0).End != data.Length)
        {
            throw new InvalidDataException("Curse texture blocks must close their complete original resource.");
        }

        void Block(ushort x, ushort y, ushort width, ushort height)
        {
            int length = checked(12 + width * height * 2);
            if (cursor > data.Length - length)
            {
                throw new InvalidDataException("Curse texture block leaves its resource.");
            }
            ReadOnlySpan<byte> block = data.AsSpan().Slice(cursor, length);
            if (BinaryPrimitives.ReadInt32LittleEndian(block) != length || BinaryPrimitives.ReadUInt16LittleEndian(block[4..]) != x || BinaryPrimitives.ReadUInt16LittleEndian(block[6..]) != y || BinaryPrimitives.ReadUInt16LittleEndian(block[8..]) != width || BinaryPrimitives.ReadUInt16LittleEndian(block[10..]) != height)
            {
                throw new InvalidDataException("Curse texture upload dimensions disagree with their original resource.");
            }
            cursor = checked(cursor + length);
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCurse47Resources? nativeCurse47Resources;

    public NativeBattleCurse47Resources ReadNativeCurse47Resources()
    {
        if (nativeCurse47Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleCurse47ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectInitializerAsset mainTim = new("mag046.tim", Read(magic, "mag046.tim").ToImmutableArray());
            nativeCurse47Resources = NativeBattleCurse47ResourcesReader.Read(executable, mainTim, files);
        }
        return nativeCurse47Resources;
    }
}
