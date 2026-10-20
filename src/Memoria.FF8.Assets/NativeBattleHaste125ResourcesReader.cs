using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleHaste125ResourcesReader
{
    public static NativeBattleHaste125Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
        return new(Source(0x1248A0C, 0x338C), Source(0x124BD98, 0x6050), new("FF8_EN:1251DE8", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1251DE8, 0xDEC)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1252BD4, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleHaste125Resources? nativeHaste125Resources;
    public NativeBattleHaste125Resources ReadNativeHaste125Resources() => nativeHaste125Resources ??= NativeBattleHaste125ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
