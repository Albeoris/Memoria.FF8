using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBlizzardResourcesReader
{
    public static NativeBattleBlizzardResources Read(byte[] executable)
    {
        return new(new("FF8_EN:DD7918", NativeBattleEffectStaticResourcesReader.Read(executable, 0xDD7918, 0x9D8)), NativeBattleEffectStaticResourcesReader.ReadSprite(executable, 0xDD82F0), NativeBattleEffectStaticResourcesReader.ReadSprite(executable, 0xDD83E4), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0xDD8590u), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0xDD9280u), NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0xDD8D48u), BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0xDD9A38, 4).AsSpan()));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBlizzardResources? nativeBlizzardResources;
    public NativeBattleBlizzardResources ReadNativeBlizzardResources() => nativeBlizzardResources ??= NativeBattleBlizzardResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
