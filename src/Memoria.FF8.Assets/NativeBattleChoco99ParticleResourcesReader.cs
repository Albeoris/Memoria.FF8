using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99ParticleResourcesReader
{
    public static NativeBattleChoco99ParticleResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleChoco99ParticleResources resources = new(Read(0x152A958), Read(0x152A31C));
        resources.Validate();
        return resources;

        NativeBattleChoco97SpriteResources Read(uint address) => new(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco99ParticleResources? nativeChoco99Particles;

    public NativeBattleChoco99ParticleResources ReadNativeChoco99ParticleResources() => nativeChoco99Particles ??= NativeBattleChoco99ParticleResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
