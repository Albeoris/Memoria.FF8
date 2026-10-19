using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleActionCameraRulesReader
{
    public static NativeBattleActionCameraRules Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeBattleActionCameraRules Read(IGameNativeMemorySource memory)
    {
        string hash = memory.SourceHash;
        NativeBattleActionCameraRules result = new(hash, Enumerable.Range(0, 5).Select(index => Word(0xB8B810u + 4u * (uint)index)).ToImmutableArray(), Enumerable.Range(0, 5).Select(index => Word(0xB8B812u + 4u * (uint)index)).ToImmutableArray(), Enumerable.Range(0, 5).Select(index => Word(0xB8B824u + 2u * (uint)index)).ToImmutableArray());
        result.Validate();
        return result;

        ushort Word(uint address)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleActionCameraRules? nativeActionCameraRules;

    public NativeBattleActionCameraRules ReadNativeActionCameraRules() => nativeActionCameraRules ??= NativeBattleActionCameraRulesReader.Read(GameDataResources.ReadNativeMemory(this.resources));
}
