using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco100ModelResourcesReader
{
    public static NativeBattleChoco97ModelResources Read(byte[] executable) => NativeBattleChoco97ModelResourcesReader.Read(new NativeExecutableMemory(executable), 0x1656DE4, 0x165BCCC, 0x1529F18);
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97ModelResources? nativeChoco100Model;

    public NativeBattleChoco97ModelResources ReadNativeChoco100ModelResources() => nativeChoco100Model ??= NativeBattleChoco100ModelResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
