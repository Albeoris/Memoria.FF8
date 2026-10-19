using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAngelWing84GlowResourcesReader
{
    public static NativeBattleAngelWing84GlowResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0x15720B0, 0x1572810 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        return new(meshes);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAngelWing84GlowResources? nativeAngelWing84GlowResources;
    public NativeBattleAngelWing84GlowResources ReadNativeAngelWing84GlowResources() => nativeAngelWing84GlowResources ??= NativeBattleAngelWing84GlowResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
