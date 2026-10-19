using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWhiteWind226ResourcesReader
{
    public static NativeBattleWhiteWind226Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
        return new(Source(0x1134B2C, 0xA30), Source(0x11340C4, 0xA68), Source(0x113202C, 0x2098), new("FF8_EN:113555C", NativeBattleEffectStaticResourcesReader.Read(memory, 0x113555C, 0x70)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x11355CC, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleWhiteWind226Resources? nativeWhiteWind226Resources;
    public NativeBattleWhiteWind226Resources ReadNativeWhiteWind226Resources() => nativeWhiteWind226Resources ??= NativeBattleWhiteWind226ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
