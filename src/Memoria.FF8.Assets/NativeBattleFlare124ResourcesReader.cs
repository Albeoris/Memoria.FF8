using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFlare124ResourcesReader
{
    public static NativeBattleFlare124Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(NativeBattleEffectStaticResourcesReader.Read(memory, NativeBattleFlare124Resources.BaseAddress, 0x12652A4 - (int)NativeBattleFlare124Resources.BaseAddress));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleFlare124Resources? nativeFlare124Resources;
    public NativeBattleFlare124Resources ReadNativeFlare124Resources() => nativeFlare124Resources ??= NativeBattleFlare124ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
