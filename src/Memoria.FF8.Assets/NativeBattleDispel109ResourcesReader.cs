using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDispel109ResourcesReader
{
    public static NativeBattleDispel109Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0x12E3600, NativeBattleEffectStaticResourcesReader.Read(memory, 0x12E3600, 0x2E10)), new("FF8_EN:12E6410", NativeBattleEffectStaticResourcesReader.Read(memory, 0x12E6410, 0x828)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x12E337C), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x12E3440), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12E6C38, 4)));
    }
}
public sealed partial class GameAssets
{
    private NativeBattleDispel109Resources? nativeDispel109Resources;
    public NativeBattleDispel109Resources ReadNativeDispel109Resources() => nativeDispel109Resources ??= NativeBattleDispel109ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
