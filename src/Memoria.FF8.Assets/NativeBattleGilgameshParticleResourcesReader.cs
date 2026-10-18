using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshParticleResourcesReader
{
    public static NativeBattleGilgameshParticleResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGilgameshParticleResources resources = new(Sprite(0xCC6730), Sprite(0xCC6954), Sprite(0xCC6F74), Sprite(0xCC6C0C));
        resources.Validate();
        return resources;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshParticleResources? nativeGilgameshParticleResources;

    public NativeBattleGilgameshParticleResources ReadNativeGilgameshParticleResources() => nativeGilgameshParticleResources ??= NativeBattleGilgameshParticleResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
