using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco100SpriteResourcesReader
{
    public static NativeBattleChoco97SpriteResources Read(byte[] executable) => NativeBattleChoco97SpriteResourcesReader.Read(new NativeExecutableMemory(executable), 0x1529804);
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97SpriteResources? nativeChoco100Sprite;

    public NativeBattleChoco97SpriteResources ReadNativeChoco100SpriteResources() => nativeChoco100Sprite ??= NativeBattleChoco100SpriteResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
