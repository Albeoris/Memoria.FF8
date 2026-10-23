using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleAdditional182ResourcesReader
{
    public static NativeBattleAdditional182Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag181.tim\0")
        {
            throw new InvalidDataException("Additional182 requires its original initializer texture name.");
        }
        ImmutableArray<NativeBattleStaticMeshResource> variants = Enumerable.Range(0, 12).Select(index => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, Dword(MeshPointersAddress + (uint)(index * 4)))).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 8).Select(index => Dword(ColorsAddress + (uint)(index * 4))).ToImmutableArray();
        NativeBattleAdditional182Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, HistoryMeshAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, PrimaryMeshAddress), variants, Sprite(SpriteAAddress), Sprite(SpriteBAddress), new("FF8_EN:D39370", memory.Read(CameraClearAddress, CameraClearLength).ToImmutableArray()), new("FF8_EN:D39FD8", memory.Read(CameraSetAddress, CameraSetLength).ToImmutableArray()), Dword(SoundClearAddress), Dword(SoundSetAddress), colors);
        resources.Validate();
        return resources;

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAdditional182Resources? nativeAdditional182Resources;
    public NativeBattleAdditional182Resources ReadNativeAdditional182Resources() => nativeAdditional182Resources ??= NativeBattleAdditional182ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
