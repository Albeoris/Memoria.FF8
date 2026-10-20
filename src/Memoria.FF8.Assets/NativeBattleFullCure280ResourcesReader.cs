using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFullCure280ResourcesReader
{
    public static NativeBattleFullCure280Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0x10A7A64, NativeBattleEffectStaticResourcesReader.Read(memory, 0x10A7A64, 0x3588)), new(0, 0x10A7974, 0, 0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x10A7974, 0xF0)), new("FF8_EN:10AAFEC", NativeBattleEffectStaticResourcesReader.Read(memory, 0x10AAFEC, 0x138)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10AB124, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleFullCure280Resources? nativeFullCure280Resources;
    public NativeBattleFullCure280Resources ReadNativeFullCure280Resources() => nativeFullCure280Resources ??= NativeBattleFullCure280ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
