using System.Collections.Immutable;
using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGraphicsRulesReader
{
    public static NativeBattleGraphicsRules Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeBattleGraphicsRules Read(IGameNativeMemorySource memory)
    {
        string hash = memory.SourceHash;
        byte[] maps = memory.Read(0xB8BA08, 48);
        NativeBattleGraphicsRules rules = new(hash, maps.AsSpan(0, 16).ToArray().Select(value => unchecked((sbyte)value)).ToImmutableArray(), maps.AsSpan(16, 32).ToArray().Select(value => unchecked((sbyte)value)).ToImmutableArray()) { UiPublicationDuration = BinaryPrimitives.ReadUInt32LittleEndian(At(0xB8A3E4, 4)), SummonColors = new(BinaryPrimitives.ReadUInt32LittleEndian(At(0xB8B7D8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(At(0xB8B9A8, 4)), BitConverter.ToSingle(At(0xB695F8, 4)), BitConverter.ToDouble(At(0xB693D8, 8))) };
        rules = rules with { ShadowSource = NativeBattleShadowSourceReader.Read(memory) };
        rules = rules with { SummonColors = rules.SummonColors! with { LinearFadeSourceB8BA94 = BinaryPrimitives.ReadUInt32LittleEndian(At(0xB8BA94, 4)) } };
        rules = rules with { ActorSounds1120 = NativeMusicCatalog.ReadBattleActorSounds(memory) };
        rules = rules with { StatusIcons = NativeBattleStatusIconResourcesReader.Read(memory) };
        byte[] victoryPairs = At(0xB8B7E0, 12).ToArray();
        rules = rules with { VictoryModelSections = Enumerable.Range(0, 6).Select(index => new NativeBattleVictoryModelSection(victoryPairs[index * 2], victoryPairs[index * 2 + 1])).ToImmutableArray() };
        rules.Validate();
        return rules;

        ReadOnlySpan<byte> At(uint address, int length)
        {
            return memory.Read(address, length);
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGraphicsRules? nativeBattleGraphicsRules;

    public NativeBattleGraphicsRules ReadNativeBattleGraphicsRules()
    {
        return nativeBattleGraphicsRules ??= NativeBattleGraphicsRulesReader.Read(GameDataResources.ReadNativeMemory(this.resources));
    }
}
