using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleAcid170Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleAcid170ResourcesReader
{
    public static NativeBattleAcid170Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag169.tim\0")
        {
            throw new InvalidDataException("Acid170 requires its original initializer texture name.");
        }
        NativeBattleAcid170Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), memory.Read(EndpointVerticesAddress, SelectedVerticesLength).ToImmutableArray(), Enumerable.Range(0, DripCountLength).Select(index => BinaryPrimitives.ReadInt32LittleEndian(memory.Read(DripCountsAddress + (uint)(index * 4), 4))).ToImmutableArray(), new("FF8_EN:D68D0C", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAcid170Resources? nativeAcid170Resources;
    public NativeBattleAcid170Resources ReadNativeAcid170Resources() => nativeAcid170Resources ??= NativeBattleAcid170ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
