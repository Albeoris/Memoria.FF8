using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSlow123ResourcesReader
{
    public static NativeBattleSlow123Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
        return new(Source(0x12652B0, 0x3A8C), Source(0x1268D3C, 0x49F4), new("FF8_EN:126D730", NativeBattleEffectStaticResourcesReader.Read(memory, 0x126D730, 0xD80)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x126E4B0, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSlow123Resources? nativeSlow123Resources;
    public NativeBattleSlow123Resources ReadNativeSlow123Resources() => nativeSlow123Resources ??= NativeBattleSlow123ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
