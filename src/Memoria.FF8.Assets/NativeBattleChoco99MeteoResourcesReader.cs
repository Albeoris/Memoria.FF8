using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99MeteoResourcesReader
{
    public static NativeBattleChoco99MeteoResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] words = memory.Read(0x152AEF0, 0x12);
        byte[] commands = memory.Read(0x152AF04, 0x10);
        NativeBattleChoco99MeteoResources resources = new(Sprite(0x152A460), new uint[] { 0x152A70C, 0x152A7D0, 0x152A894 }.Select(Sprite).ToImmutableArray(), new(0x152AEF0, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(words.AsSpan(index * 2))).ToImmutableArray()), new(0x152AF04, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(index * 4))).ToImmutableArray()));
        resources.Validate();
        return resources;

        NativeBattleChoco97SpriteResources Sprite(uint address) => new(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco99MeteoResources? nativeChoco99Meteo;

    public NativeBattleChoco99MeteoResources ReadNativeChoco99MeteoResources() => nativeChoco99Meteo ??= NativeBattleChoco99MeteoResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
