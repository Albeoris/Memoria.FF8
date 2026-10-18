using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco98ModelResourcesReader
{
    public static NativeBattleChoco97ModelResources Read(byte[] executable)
    {
        return NativeBattleChoco97ModelResourcesReader.Read(new NativeExecutableMemory(executable), 0x167482C, 0x1679714, 0x152B580);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97ModelResources? nativeChoco98Model;

    public NativeBattleChoco97ModelResources ReadNativeChoco98ModelResources() => nativeChoco98Model ??= NativeBattleChoco98ModelResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
