using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleGatlingGun150ResourcesReader
{
    public static NativeBattleGatlingGun150Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag149.tim\0")
        {
            throw new InvalidDataException("Gatling Gun requires its original initializer texture name.");
        }
        NativeBattleGatlingGun150Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshCAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), new("FF8_EN:DC1DD4", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGatlingGun150Resources? nativeGatlingGun150Resources;
    public NativeBattleGatlingGun150Resources ReadNativeGatlingGun150Resources() => nativeGatlingGun150Resources ??= NativeBattleGatlingGun150ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
