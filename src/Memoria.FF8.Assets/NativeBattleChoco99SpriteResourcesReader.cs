using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99SpriteResourcesReader
{
    public static NativeBattleChoco97SpriteResources Read(byte[] executable) => NativeBattleChoco97SpriteResourcesReader.Read(new NativeExecutableMemory(executable), 0x1529FB4);
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97SpriteResources? nativeChoco99Sprite;

    public NativeBattleChoco97SpriteResources ReadNativeChoco99SpriteResources() => nativeChoco99Sprite ??= NativeBattleChoco99SpriteResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
