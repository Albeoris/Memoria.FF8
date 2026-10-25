using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBlizzardResourcesReader
{
    public static NativeBattleBlizzardResources Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeBattleBlizzardResources Read(IGameNativeMemorySource memory)
    {
        return new(new("FF8_EN:DD7918", NativeBattleEffectStaticResourcesReader.Read(memory, 0xDD7918, 0x9D8)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDD82F0), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDD83E4), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDD8590u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDD9280u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDD8D48u), BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(memory, 0xDD9A38, 4).AsSpan()));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBlizzardResources? nativeBlizzardResources;
    public NativeBattleBlizzardResources ReadNativeBlizzardResources() => nativeBlizzardResources ??= NativeBattleBlizzardResourcesReader.Read(ReadNativeMemory());
}
