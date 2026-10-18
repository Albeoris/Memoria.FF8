using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco98SpriteResourcesReader
{
    public static NativeBattleChoco97SpriteResources Read(byte[] executable) => NativeBattleChoco97SpriteResourcesReader.Read(new NativeExecutableMemory(executable), 0x152AF40);
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97SpriteResources? nativeChoco98Sprite;

    public NativeBattleChoco97SpriteResources ReadNativeChoco98SpriteResources() => nativeChoco98Sprite ??= NativeBattleChoco98SpriteResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
