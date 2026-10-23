using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleAdditional181ResourcesReader
{
    public static NativeBattleAdditional181Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag180.tim\0")
        {
            throw new InvalidDataException("Additional181 requires its original initializer texture name.");
        }
        NativeBattleAdditional181Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), new("FF8_EN:D3D3F0", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAdditional181Resources? nativeAdditional181Resources;
    public NativeBattleAdditional181Resources ReadNativeAdditional181Resources() => nativeAdditional181Resources ??= NativeBattleAdditional181ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
