using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDarkMist13ResourcesReader
{
    public static NativeBattleDarkMist13Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        ImmutableArray<NativeBattleVector> vectors = Enumerable.Range(0, 10).Select(index =>
        {
            byte[] data = memory.Read(0x1634B20u + (uint)index * 8, 6);
            return new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(4)));
        }).ToImmutableArray();
        return new(Read(0x163257C, 0x16A8), Read(0x1630ED4, 8 + 144 * 8), Read(0x1633C24, 0xE28), [NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x16309FC), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x1630E94), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x1630C78), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x1630D84)], vectors, new("FF8_EN:1634A4C", Read(0x1634A4C, 0xD0)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x16309F8, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDarkMist13Resources? nativeDarkMist13Resources;
    public NativeBattleDarkMist13Resources ReadNativeDarkMist13Resources() => nativeDarkMist13Resources ??= NativeBattleDarkMist13ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
