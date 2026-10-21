using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDrawStock15ParticleResourcesReader
{
    public static NativeBattleDrawStock15ParticleResources Read(byte[] executable)
    {
        NativeBattleDrawStock15ParticleResources result = new(NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x162B520), NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x162B730));
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDrawStock15ParticleResources? nativeDrawStock15ParticleResources;

    public NativeBattleDrawStock15ParticleResources ReadNativeDrawStock15ParticleResources()
    {
        return nativeDrawStock15ParticleResources ??= NativeBattleDrawStock15ParticleResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
