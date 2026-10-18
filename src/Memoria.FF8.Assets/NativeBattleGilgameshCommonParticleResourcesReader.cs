using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshCommonParticleResourcesReader
{
    public static NativeBattleGilgameshCommonParticleResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGilgameshCommonParticleResources resources = new(new(0, 0xCC6DC8, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xCC6DC8)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCCB1Cu));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshCommonParticleResources? nativeGilgameshCommonParticleResources;

    public NativeBattleGilgameshCommonParticleResources ReadNativeGilgameshCommonParticleResources() => nativeGilgameshCommonParticleResources ??= NativeBattleGilgameshCommonParticleResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
