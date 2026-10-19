using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBadBreath113ResourcesReader
{
    public static NativeBattleBadBreath113Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        ImmutableArray<short> wave = Enumerable.Range(0, 4096).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(0x12C1604u + (uint)index * 2, 2))).ToImmutableArray();
        ImmutableArray<uint> flags = Enumerable.Range(0, 2).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12C3604u + (uint)index * 4, 4))).ToImmutableArray();
        return new(new(0x12C0244, Read(0x12C0244, 0xDD0)), flags, [NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x12C118C), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x12C1418), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x12C1014)], new("FF8_EN:12C1590", Read(0x12C1590, 0x70)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12C1600, 4)), wave);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBadBreath113Resources? nativeBadBreath113Resources;
    public NativeBattleBadBreath113Resources ReadNativeBadBreath113Resources() => nativeBadBreath113Resources ??= NativeBattleBadBreath113ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
