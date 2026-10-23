using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleCannonBlow151Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleCannonBlow151ResourcesReader
{
    public static NativeBattleCannonBlow151Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag150.tim\0")
        {
            throw new InvalidDataException("Cannon Blow requires its original initializer texture name.");
        }
        NativeBattleCannonBlow151Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshCAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshDAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, ScrollingMeshAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), Sprite(SpriteDAddress), Sprite(SpriteEAddress), new("FF8_EN:DBEC8C", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCannonBlow151Resources? nativeCannonBlow151Resources;
    public NativeBattleCannonBlow151Resources ReadNativeCannonBlow151Resources() => nativeCannonBlow151Resources ??= NativeBattleCannonBlow151ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
