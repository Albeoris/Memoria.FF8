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
            byte[] executable = File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe"));
            nativeEffectMeshes = [NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 1), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 4), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 7)];
        }
        return nativeEffectMeshes;
    }
}
