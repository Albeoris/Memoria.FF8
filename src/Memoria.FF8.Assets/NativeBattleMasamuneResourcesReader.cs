using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMasamuneResourcesReader
{
    public static NativeBattleMasamuneResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleMasamuneResources result = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCF2DCu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCC72FCu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCC877Cu), new(0, 0xCC7120, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xCC7120)), new(0, 0xCC6DC8, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xCC6DC8)));
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMasamuneResources? nativeMasamuneResources;

    public NativeBattleMasamuneResources ReadNativeMasamuneResources()
    {
        return nativeMasamuneResources ??= NativeBattleMasamuneResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
