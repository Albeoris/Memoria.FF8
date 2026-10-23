using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBreath157Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleBreath157ResourcesReader
{
    public static NativeBattleBreath157Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag156.tim\0")
        {
            throw new InvalidDataException("Breath requires its original initializer texture name.");
        }
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleBreath157Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshCAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshDAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshEAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), Sprite(SpriteDAddress), new("FF8_EN:DB0828", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAAddress, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundBAddress, 4)), new(checked((short)BinaryPrimitives.ReadInt32LittleEndian(memory.Read(ReferenceDirectionAddress, 4))), checked((short)BinaryPrimitives.ReadInt32LittleEndian(memory.Read(ReferenceDirectionAddress + 4, 4))), checked((short)BinaryPrimitives.ReadInt32LittleEndian(memory.Read(ReferenceDirectionAddress + 8, 4)))));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBreath157Resources? nativeBreath157Resources;
    public NativeBattleBreath157Resources ReadNativeBreath157Resources() => nativeBreath157Resources ??= NativeBattleBreath157ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
