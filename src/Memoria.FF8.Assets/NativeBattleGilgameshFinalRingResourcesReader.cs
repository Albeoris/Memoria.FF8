using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshFinalRingResourcesReader
{
    public static NativeBattleStaticMeshResource Read(byte[] executable)
    {
        NativeBattleStaticMeshResource resource = NativeBattleEffectStaticResourcesReader.ReadMesh(new NativeExecutableMemory(executable), 0xCCFAB4u);
        if (resource.Bytes.Length != 0x3A0 || NativeBattleIceMeshVertices.Group(resource.Bytes.AsSpan(), 3).Count != 24)
        {
            throw new InvalidDataException("Native Gilgamesh final ring requires its complete original GT4 mesh.");
        }
        return resource;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleStaticMeshResource? nativeGilgameshFinalRingResource;

    public NativeBattleStaticMeshResource ReadNativeGilgameshFinalRingResource() => nativeGilgameshFinalRingResource ??= NativeBattleGilgameshFinalRingResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
