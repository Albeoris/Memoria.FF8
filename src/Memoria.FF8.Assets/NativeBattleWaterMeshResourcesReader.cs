using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWaterMeshResourcesReader
{
    public static NativeBattleEffectHeapAllocation ReadResourceMap(byte[] executable)
    {
        NativeBattleWater222ResourcesReader.Binding(executable);
        return new("FF8_EN:186C8CC", NativeBattleEffectStaticResourcesReader.Read(executable, 0x186C8CC, 0x10));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffectHeapAllocation? nativeWaterMeshResourceMap;

    public NativeBattleEffectHeapAllocation ReadNativeWaterMeshResourceMap()
    {
        if (nativeWaterMeshResourceMap is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            nativeWaterMeshResourceMap = NativeBattleWaterMeshResourcesReader.ReadResourceMap(executable);
        }
        return nativeWaterMeshResourceMap;
    }
}
