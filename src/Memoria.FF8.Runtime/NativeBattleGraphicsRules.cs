using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGraphicsRules(string ExecutableSha256, ImmutableArray<sbyte> SavedStatusMap, ImmutableArray<sbyte> BattleStatusMap)
{
    public uint? UiPublicationDuration { get; init; }
    public NativeBattleSummonColors? SummonColors { get; init; }
    public void Validate()
    {
        if (ExecutableSha256 != NativeFieldRuntimeProfile.FF8EnExecutableHash || SavedStatusMap.IsDefault || SavedStatusMap.Length != 16 || BattleStatusMap.IsDefault || BattleStatusMap.Length != 32 || SavedStatusMap.Concat(BattleStatusMap).Any(value => value is < -1 or > 31))
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
