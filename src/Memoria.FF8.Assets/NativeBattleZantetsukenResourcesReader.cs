using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleZantetsukenResourcesReader
{
    public static NativeBattleZantetsukenResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<uint>.Builder indices = ImmutableArray.CreateBuilder<uint>(56);
        for (uint index = 0; index < 56; index++)
        {
            indices.Add(ReadWord(0xCD04C0 + index * 4));
        }
        ImmutableArray<NativeBattleZantetsukenStageOrdering>.Builder ordering = ImmutableArray.CreateBuilder<NativeBattleZantetsukenStageOrdering>(4);
        for (uint index = 0; index < 4; index++)
        {
            ordering.Add(new(ReadWord(0xCD0970 + index * 8), ReadWord(0xCD0974 + index * 8)));
        }
        NativeBattleZantetsukenResources result = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCCDE04), indices.MoveToImmutable(), ordering.MoveToImmutable());
        result.Validate();
        return result;

        uint ReadWord(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleZantetsukenResources? nativeZantetsukenResources;

    public NativeBattleZantetsukenResources ReadNativeZantetsukenResources()
    {
        return nativeZantetsukenResources ??= NativeBattleZantetsukenResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
