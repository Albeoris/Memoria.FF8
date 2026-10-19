using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSuicide180ResourcesReader
{
    public static NativeBattleSuicide180Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:D413B8", memory.Read(0xD413B8, 0x110).ToImmutableArray()), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD4353C), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD417D4), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD4380C), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD41674), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD414C8), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD445B4, 4)));
    }
}
public sealed partial class GameAssets
{
    private NativeBattleSuicide180Resources? nativeSuicide180Resources;
    public NativeBattleSuicide180Resources ReadNativeSuicide180Resources() => nativeSuicide180Resources ??= NativeBattleSuicide180ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
