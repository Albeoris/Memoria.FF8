using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBombGlow179ResourcesReader
{
    public static NativeBattleBombGlow179Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:D445C8", memory.Read(0xD445C8, 0x138).ToImmutableArray()), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD44700), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD44968, 4)), Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD44970u + (uint)index * 4, 4))).ToImmutableArray());
    }
}
public sealed partial class GameAssets
{
    private NativeBattleBombGlow179Resources? nativeBombGlow179Resources;
    public NativeBattleBombGlow179Resources ReadNativeBombGlow179Resources() => nativeBombGlow179Resources ??= NativeBattleBombGlow179ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
