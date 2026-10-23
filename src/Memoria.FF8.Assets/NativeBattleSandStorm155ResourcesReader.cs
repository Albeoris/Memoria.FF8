using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSandStorm155Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleSandStorm155ResourcesReader
{
    public static NativeBattleSandStorm155Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag154.tim\0")
        {
            throw new InvalidDataException("Sand Storm requires its original initializer texture name.");
        }
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleSandStorm155Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), new("FF8_EN:DB7DC4", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSandStorm155Resources? nativeSandStorm155Resources;
    public NativeBattleSandStorm155Resources ReadNativeSandStorm155Resources() => nativeSandStorm155Resources ??= NativeBattleSandStorm155ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
