using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshPointResourcesReader
{
    public static NativeBattleGilgameshPointResources Read(byte[] executable)
    {
        NativeBattleGilgameshPointResources resources = new(new("FF8_EN:CD0428", NativeBattleEffectStaticResourcesReader.Read(new NativeExecutableMemory(executable), 0xCD0428, 64)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshPointResources? nativeGilgameshPointResources;

    public NativeBattleGilgameshPointResources ReadNativeGilgameshPointResources() => nativeGilgameshPointResources ??= NativeBattleGilgameshPointResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
