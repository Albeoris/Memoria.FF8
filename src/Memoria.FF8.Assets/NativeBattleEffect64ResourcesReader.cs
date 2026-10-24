using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect64ResourcesReader
{
    public static NativeBattleEffect64Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag063.tim\0")
        {
            throw new InvalidDataException("Effect64 initializer requires its original texture name.");
        }
        NativeBattleEffect64Resources result = new(mainTim, new("FF8_EN:1342D40", Bytes(CameraAddress, CameraLength)), new("FF8_EN:1342EF8", Bytes(VibrationAddress, VibrationLength)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), ProgrammeAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ProgrammeLengths[index]))).ToImmutableArray(), SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address), Bytes(ProgrammeDirectoryAddress, 24), ScriptLengths.ToImmutableDictionary(pair => pair.Key, pair => Bytes(pair.Key, pair.Value)));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect64Resources? nativeEffect64Resources;

    public NativeBattleEffect64Resources ReadNativeEffect64Resources()
    {
        if (nativeEffect64Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect64Resources = NativeBattleEffect64ResourcesReader.Read(ReadNativeExecutable(), new("mag063.tim", Read(magic, "mag063.tim").ToImmutableArray()));
        }
        return nativeEffect64Resources;
    }
}
