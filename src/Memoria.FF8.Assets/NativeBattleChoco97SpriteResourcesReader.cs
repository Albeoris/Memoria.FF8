using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco97SpriteResourcesReader
{
    public static NativeBattleChoco97SpriteResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint address = 0x152B5C8;
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address);
        if (bytes.Length != 0x368 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(8)) != 20)
        {
            throw new InvalidDataException("Native Choco projected sprite leaves its original twenty-frame extent.");
        }
        return new(address, bytes);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97SpriteResources? nativeChoco97Sprite;

    public NativeBattleChoco97SpriteResources ReadNativeChoco97SpriteResources() => nativeChoco97Sprite ??= NativeBattleChoco97SpriteResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
