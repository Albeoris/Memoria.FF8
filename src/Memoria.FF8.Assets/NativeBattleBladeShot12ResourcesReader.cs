using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleBladeShot12ResourcesReader
{
    public static NativeBattleBladeShot12Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag011.tim\0")
        {
            throw new InvalidDataException("Blade Shot12 requires its original initializer texture name.");
        }
        NativeBattleBladeShot12Resources resources = new(Mesh(MeshAAddress), Mesh(MeshBAddress), Mesh(MeshCAddress), Mesh(MeshDAddress), Mesh(MeshEAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), Sprite(SpriteDAddress), Sprite(SpriteEAddress), Source(MorphSourceA0), Source(MorphSourceA1), Source(MorphSourceB0), Source(MorphSourceB1), new("FF8_EN:1365634", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(OpeningSoundAddress, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(EndingSoundAddress, 4)));
        resources.Validate();
        return resources;

        NativeBattleStaticMeshResource Mesh(uint address) => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleModuleResource Source(uint address) => new(0, address, 0, 0, memory.Read(address, MorphSourceLength).ToImmutableArray());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBladeShot12Resources? nativeBladeShot12Resources;
    public NativeBattleBladeShot12Resources ReadNativeBladeShot12Resources() => nativeBladeShot12Resources ??= NativeBattleBladeShot12ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
