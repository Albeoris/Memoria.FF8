using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleJunk44ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 360, 361 });

    public static NativeBattleJunk44Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag043.tim\0")
        {
            throw new InvalidDataException("Junk requires its original initializer texture identity.");
        }
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 2 || files.Where((file, index) => file is null || file.FileId != names[index].FileId || !string.Equals(file.Name, names[index].Name, StringComparison.OrdinalIgnoreCase)).Any())
        {
            throw new InvalidDataException("Junk completion files disagree with the original executable directory.");
        }
        uint[] addresses = [0x1354EB8, 0x1356250, 0x135BD28, 0x135E934, 0x13605BC, 0x1356B68, 0x1357FD8, 0x1359448, 0x135A8B8, 0x1360FF4];
        NativeBattleJunk44Resources result = new(new(ProgrammeAddress, memory.Read(ProgrammeAddress, ProgrammeLength).ToImmutableArray()), new(0, SpriteAddress, 0, 0, memory.Read(SpriteAddress, SpriteLength).ToImmutableArray()), memory.Read(DebrisTableAddress, 168).ToImmutableArray(), addresses.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray(), new("FF8_EN:1354A30", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), files);
        result.Validate();
        for (int index = 0; index < files.Length; index++)
        {
            ValidateTim(files[index].Data, checked((ushort)(704 + index * 64)));
        }
        return result;
    }

    private static void ValidateTim(ImmutableArray<byte> data, ushort imageX)
    {
        if (data.IsDefault || data.Length != 33824 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(4)) != 8)
        {
            throw new InvalidDataException("Junk completion requires its original indexed texture format.");
        }
        int cursor = 8;
        Block(624, 224, 16, 32);
        Block(imageX, 256, 64, 256);
        if (cursor != data.Length || WorldAssetReader.ReadTim(data.ToArray(), 0).End != data.Length)
        {
            throw new InvalidDataException("Junk completion texture must close its complete resource.");
        }

        void Block(ushort x, ushort y, ushort width, ushort height)
        {
            int length = checked(12 + width * height * 2);
            ReadOnlySpan<byte> block = data.AsSpan().Slice(cursor, length);
            if (BinaryPrimitives.ReadInt32LittleEndian(block) != length || BinaryPrimitives.ReadUInt16LittleEndian(block[4..]) != x || BinaryPrimitives.ReadUInt16LittleEndian(block[6..]) != y || BinaryPrimitives.ReadUInt16LittleEndian(block[8..]) != width || BinaryPrimitives.ReadUInt16LittleEndian(block[10..]) != height)
            {
                throw new InvalidDataException("Junk completion texture has an unsupported upload rectangle.");
            }
            cursor = checked(cursor + length);
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleJunk44Resources? nativeJunk44Resources;

    public NativeBattleJunk44Resources ReadNativeJunk44Resources()
    {
        if (nativeJunk44Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleJunk44ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeJunk44Resources = NativeBattleJunk44ResourcesReader.Read(executable, files);
        }
        return nativeJunk44Resources;
    }
}
