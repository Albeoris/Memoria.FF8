using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleSnipeLaser302ResourcesReader
{
    public static NativeBattleSnipeLaser302Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 15)) != "mag276-301.tim\0")
        {
            throw new InvalidDataException("Snipe Laser requires its original initializer texture identity.");
        }
        NativeBattleSnipeLaser302Resources resources = new(new(ProgrammeAAddress, memory.Read(ProgrammeAAddress, ProgrammeALength).ToImmutableArray()), new(ProgrammeBAddress, memory.Read(ProgrammeBAddress, ProgrammeBLength).ToImmutableArray()), new(ProgrammeCAddress, memory.Read(ProgrammeCAddress, ProgrammeCLength).ToImmutableArray()), new(ProgrammeDAddress, memory.Read(ProgrammeDAddress, ProgrammeDLength).ToImmutableArray()), new(0, StaticMeshAddress, memory.Read(StaticMeshAddress, StaticMeshLength).ToImmutableArray()), new(0, SpriteAAddress, 0, 0, memory.Read(SpriteAAddress, SpriteALength).ToImmutableArray()), new(0, SpriteBAddress, 0, 0, memory.Read(SpriteBAddress, SpriteBLength).ToImmutableArray()), new("FF8_EN:103B914", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), memory.Read(SourcePointsAddress, 16).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSnipeLaser302Resources? nativeSnipeLaser302Resources;
    public NativeBattleSnipeLaser302Resources ReadNativeSnipeLaser302Resources() => nativeSnipeLaser302Resources ??= NativeBattleSnipeLaser302ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
