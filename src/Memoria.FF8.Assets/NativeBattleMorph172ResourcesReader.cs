using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMorph172Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleMorph172ResourcesReader
{
    public static NativeBattleMorph172Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag171.tim\0")
        {
            throw new InvalidDataException("Morph172 requires its original initializer texture name.");
        }
        NativeBattleMorph172Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), Sprite(SpriteAAddress), Enumerable.Range(0, ColorCount).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(ColorsAddress + (uint)(index * 4), 4))).ToImmutableArray(), new("FF8_EN:D61E28", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMorph172Resources? nativeMorph172Resources;
    public NativeBattleMorph172Resources ReadNativeMorph172Resources() => nativeMorph172Resources ??= NativeBattleMorph172ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
