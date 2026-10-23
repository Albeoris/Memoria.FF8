using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect283ResourcesReader
{
    public static NativeBattleEffect283Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag282.tim\0")
        {
            throw new InvalidDataException("Effect283 requires its original initializer texture identity.");
        }
        byte[] palette = memory.Read(PaletteAddress, 32);
        NativeBattleEffect283Resources result = new(new(MainProgrammeAddress, Bytes(MainProgrammeAddress, MainProgrammeLength)), new(SecondaryProgrammeAddress, Bytes(SecondaryProgrammeAddress, SecondaryProgrammeLength)), new("FF8_EN:1094FC0", Bytes(CameraAddress, CameraLength)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(palette.AsSpan(index * 4, 4))).ToImmutableArray(), Bytes(WarpTablesAddress, WarpTablesLength));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect283Resources? nativeEffect283Resources;

    public NativeBattleEffect283Resources ReadNativeEffect283Resources() => nativeEffect283Resources ??= NativeBattleEffect283ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
