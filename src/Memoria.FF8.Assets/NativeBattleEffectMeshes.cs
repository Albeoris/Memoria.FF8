using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private ImmutableArray<NativeBattleStaticMeshResource> nativeEffectMeshes;

    public ImmutableArray<NativeBattleStaticMeshResource> ReadNativeEffectMeshes()
    {
        if (nativeEffectMeshes.IsDefault)
        {
            _ = GameDataResources.RequireNativeRuntimeProfile(resources);
            IGameNativeMemorySource memory = ReadNativeMemory();
            nativeEffectMeshes = [NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 1), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 2), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 3), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 4), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 5), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 7)];
        }
        return nativeEffectMeshes;
    }
}
