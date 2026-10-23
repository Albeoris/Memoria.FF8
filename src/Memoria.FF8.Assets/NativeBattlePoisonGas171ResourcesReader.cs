using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattlePoisonGas171Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattlePoisonGas171ResourcesReader
{
    public static NativeBattlePoisonGas171Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag170.tim\0")
        {
            throw new InvalidDataException("PoisonGas171 requires its original initializer texture name.");
        }
        NativeBattlePoisonGas171Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), memory.Read(EmitterPointsAddress, EmitterPointsLength).ToImmutableArray(), new("FF8_EN:D65640", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattlePoisonGas171Resources? nativePoisonGas171Resources;
    public NativeBattlePoisonGas171Resources ReadNativePoisonGas171Resources() => nativePoisonGas171Resources ??= NativeBattlePoisonGas171ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
