using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDrawStock15ResourcesReader
{
    public static NativeBattleDrawStock15Resources Read(byte[] executable)
    {
        NativeBattleEffectHeapAllocation animation = new("FF8_EN:162AD54", NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleDrawStock15Profile.CameraAddress, 0x7CC));
        NativeBattleDrawStock15Resources result = new(animation, NativeBattleDrawStock15NodeResourcesReader.Read(executable), NativeBattleDrawStock15ParticleResourcesReader.Read(executable));
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDrawStock15Resources? nativeDrawStock15Resources;

    public NativeBattleDrawStock15Resources ReadNativeDrawStock15Resources()
    {
        return nativeDrawStock15Resources ??= NativeBattleDrawStock15ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
