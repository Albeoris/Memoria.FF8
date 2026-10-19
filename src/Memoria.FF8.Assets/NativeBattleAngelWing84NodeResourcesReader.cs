using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAngelWing84NodeResourcesReader
{
    public static NativeBattleAngelWing84NodeResources Read(byte[] executable)
    {
        NativeBattleAngelWing84NodeResources resources = new(NativeBattleEffectStaticResourcesReader.Read(new NativeExecutableMemory(executable), NativeBattleAngelWing84NodeResources.Address, NativeBattleAngelWing84NodeResources.Length));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAngelWing84NodeResources? nativeAngelWing84NodeResources;
    public NativeBattleAngelWing84NodeResources ReadNativeAngelWing84NodeResources() => nativeAngelWing84NodeResources ??= NativeBattleAngelWing84NodeResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
