using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleVictoryModelSection(byte Model, byte Section);

public sealed record NativeBattleGraphicsRules(string ExecutableSha256, ImmutableArray<sbyte> SavedStatusMap, ImmutableArray<sbyte> BattleStatusMap)
{
    public ImmutableArray<NativeBattleVictoryModelSection>? VictoryModelSections { get; init; }
    public uint? UiPublicationDuration { get; init; }
    public NativeBattleSummonColors? SummonColors { get; init; }
    public NativeBattleShadowSource? ShadowSource { get; init; }
    public ImmutableArray<uint>? ActorSounds1120 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStatusIconResources? StatusIcons { get; init; }
    public void Validate()
    {
        StatusIcons?.Validate();
        if (VictoryModelSections is { } victory && (victory.IsDefault || victory.Length != 6 || victory.Any(row => row is null || row.Model >= 16 || row.Section is < 2 or > 7) || victory.Select(row => row.Model).Distinct().Count() != 6))
        {
            throw new InvalidDataException("Native503040 requires its six authenticated model/section BYTE pairs.");
        }
        if (ActorSounds1120 is { } sounds && (sounds.IsDefault || sounds.Length != 160 * 7))
        {
            throw new InvalidDataException("Native5015A0 requires the complete B8A418 seven-DWORD table for160 actor identities.");
        }
        if ((ExecutableSha256 != NativeFieldRuntimeProfile.FF8EnExecutableHash && ExecutableSha256 != NativeFieldRuntimeProfile.RemasteredEnImageHash) || SavedStatusMap.IsDefault || SavedStatusMap.Length != 16 || BattleStatusMap.IsDefault || BattleStatusMap.Length != 32 || SavedStatusMap.Concat(BattleStatusMap).Any(value => value is < -1 or > 31))
        {
            throw new InvalidDataException("Native graphics status projection requires the authenticated16/32-byte executable maps.");
        }
    }

    public uint Project(ushort saved, uint battle)
    {
        Validate();
        uint result = 0;
        for (int bit = 0; bit < 16; bit++)
        {
            if ((saved & (1 << bit)) != 0 && SavedStatusMap[bit] >= 0)
            {
                result |= 1u << SavedStatusMap[bit];
            }
        }
        for (int bit = 0; bit < 32; bit++)
        {
            if ((battle & (1u << bit)) != 0 && BattleStatusMap[bit] >= 0)
            {
                result |= 1u << BattleStatusMap[bit];
            }
        }
        return result;
    }
}
