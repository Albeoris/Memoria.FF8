using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleWildCannonBlow156ResourcesReader
{
    public static NativeBattleWildCannonBlow156Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag150.tim\0")
        {
            throw new InvalidDataException("Wild Cannon Blow requires its original initializer texture name.");
        }
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleWildCannonBlow156Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshCAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshDAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), Sprite(SpriteDAddress), Sprite(SpriteEAddress), new("FF8_EN:DB4CD4", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAAddress, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundBAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleWildCannonBlow156Resources? nativeWildCannonBlow156Resources;
    public NativeBattleWildCannonBlow156Resources ReadNativeWildCannonBlow156Resources() => nativeWildCannonBlow156Resources ??= NativeBattleWildCannonBlow156ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
