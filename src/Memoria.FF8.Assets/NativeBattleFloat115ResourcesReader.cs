using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFloat115ResourcesReader
{
    public static NativeBattleFloat115Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
        return new(Source(0x12B5780, 0x1FE8), Source(0x12B7768, 0x21C0), new("FF8_EN:12B9928", NativeBattleEffectStaticResourcesReader.Read(memory, 0x12B9928, 0x9CC)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12BA2F4, 4)), new(BinaryPrimitives.ReadInt16LittleEndian(memory.Read(0x12BA2F8, 2)), BinaryPrimitives.ReadInt16LittleEndian(memory.Read(0x12BA2FA, 2)), BinaryPrimitives.ReadInt16LittleEndian(memory.Read(0x12BA2FC, 2))));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleFloat115Resources? nativeFloat115Resources;
    public NativeBattleFloat115Resources ReadNativeFloat115Resources() => nativeFloat115Resources ??= NativeBattleFloat115ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
