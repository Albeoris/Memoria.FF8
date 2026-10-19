using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSleep145ResourcesReader
{
    public static NativeBattleSleep145Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(NativeBattleEffectStaticResourcesReader.Read(memory, 0xDD746C, 0x498), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDD6FF0), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDD7114), new("FF8_EN:DD67B4", NativeBattleEffectStaticResourcesReader.Read(memory, 0xDD67B4, 0x83C)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDD7904, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSleep145Resources? nativeSleep145Resources;
    public NativeBattleSleep145Resources ReadNativeSleep145Resources() => nativeSleep145Resources ??= NativeBattleSleep145ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
