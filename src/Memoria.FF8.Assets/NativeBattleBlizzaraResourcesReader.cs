using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBlizzaraResourcesReader
{
    public static NativeBattleBlizzaraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:131DA00", NativeBattleEffectStaticResourcesReader.Read(memory, 0x131DA00, 0xD50)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x13197E4), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1319990), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x131A678), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x131ABB0), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x131C86C), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x131E750, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBlizzaraResources? nativeBlizzaraResources;
    public NativeBattleBlizzaraResources ReadNativeBlizzaraResources() => nativeBlizzaraResources ??= NativeBattleBlizzaraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
