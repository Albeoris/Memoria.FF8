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
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            nativeEffectMeshes = [NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 1), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 2), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 3), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 4), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 7)];
        }
        return nativeEffectMeshes;
    }
}
