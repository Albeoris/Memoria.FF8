using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleShotgun281ResourcesReader
{
    public static NativeBattleShotgun281Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag280.tim\0")
        {
            throw new InvalidDataException("Shotgun requires its original initializer texture identity.");
        }
        NativeBattleShotgun281Resources resources = new(new(ProgrammeAAddress, memory.Read(ProgrammeAAddress, ProgrammeALength).ToImmutableArray()), new(ProgrammeBAddress, memory.Read(ProgrammeBAddress, ProgrammeBLength).ToImmutableArray()), new(0, SpriteAAddress, 0, 0, memory.Read(SpriteAAddress, SpriteALength).ToImmutableArray()), new(1, SpriteBAddress, 0, 0, memory.Read(SpriteBAddress, SpriteBLength).ToImmutableArray()), new("FF8_EN:10A7824", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), memory.Read(MuzzlePointAddress, 8).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleShotgun281Resources? nativeShotgun281Resources;
    public NativeBattleShotgun281Resources ReadNativeShotgun281Resources() => nativeShotgun281Resources ??= NativeBattleShotgun281ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
