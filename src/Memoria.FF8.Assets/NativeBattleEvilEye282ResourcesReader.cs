using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEvilEye282ResourcesReader
{
    public static NativeBattleEvilEye282Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag281.tim\0")
        {
            throw new InvalidDataException("Evil-Eye requires its original initializer texture identity.");
        }
        NativeBattleEvilEye282Resources resources = new(new(ProgrammeAAddress, memory.Read(ProgrammeAAddress, ProgrammeALength).ToImmutableArray()), new(ProgrammeBAddress, memory.Read(ProgrammeBAddress, ProgrammeBLength).ToImmutableArray()), new(0, SpriteAddress, 0, 0, memory.Read(SpriteAddress, SpriteLength).ToImmutableArray()), new("FF8_EN:10A2F54", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), memory.Read(SourcePointAddress, 8).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEvilEye282Resources? nativeEvilEye282Resources;
    public NativeBattleEvilEye282Resources ReadNativeEvilEye282Resources() => nativeEvilEye282Resources ??= NativeBattleEvilEye282ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
