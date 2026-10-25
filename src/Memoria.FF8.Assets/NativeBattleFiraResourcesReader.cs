using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFiraResourcesReader
{
    public static NativeBattleFiraResources Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeBattleFiraResources Read(IGameNativeMemorySource memory)
    {
        ReadOnlySpan<byte> uvOffsets = memory.Read(0xDDF454, 16 * 4);
        ImmutableArray<uint>.Builder table = ImmutableArray.CreateBuilder<uint>(16);
        for (int index = 0; index < 16; index++)
        {
            table.Add(BinaryPrimitives.ReadUInt32LittleEndian(uvOffsets[(index * 4)..]));
        }
        return new(new("FF8_EN:DDD904", NativeBattleEffectStaticResourcesReader.Read(memory, 0xDDD904, 0x7D4)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDDE0D8), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDDE100), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDDE2ACu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDDE6C4u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDDEF14u), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDDF44C, 4)), table.MoveToImmutable());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleFiraResources? nativeFiraResources;
    public NativeBattleFiraResources ReadNativeFiraResources() => nativeFiraResources ??= NativeBattleFiraResourcesReader.Read(ReadNativeMemory());
}
