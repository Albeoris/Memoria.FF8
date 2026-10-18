using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshOpeningResourcesReader
{
    public static NativeBattleGilgameshOpeningResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGilgameshOpeningResources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCC8F5Cu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCB92Cu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCC434u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCA14Cu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCAD3Cu));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshOpeningResources? nativeGilgameshOpeningResources;

    public NativeBattleGilgameshOpeningResources ReadNativeGilgameshOpeningResources() => nativeGilgameshOpeningResources ??= NativeBattleGilgameshOpeningResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
