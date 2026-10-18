using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99ModelResourcesReader
{
    public static NativeBattleChoco97ModelResources Read(byte[] executable) => NativeBattleChoco97ModelResourcesReader.Read(new NativeExecutableMemory(executable), 0x1665B08, 0x16704DC, 0x152AED0);
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97ModelResources? nativeChoco99Model;

    public NativeBattleChoco97ModelResources ReadNativeChoco99ModelResources() => nativeChoco99Model ??= NativeBattleChoco99ModelResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
