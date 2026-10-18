using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshFlareResourcesReader
{
    public static NativeBattleGilgameshFlareResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGilgameshFlareResources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCE5DCu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCEC5Cu));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshFlareResources? nativeGilgameshFlareResources;

    public NativeBattleGilgameshFlareResources ReadNativeGilgameshFlareResources() => nativeGilgameshFlareResources ??= NativeBattleGilgameshFlareResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
