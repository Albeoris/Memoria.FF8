using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeExperienceReceipt([property: JsonRequired] NativeBattleExperienceBanks Banks, [property: JsonRequired] ImmutableArray<byte> PartyRows)
{
    public void Validate(BattleResult result)
    {
        Banks.Validate();
        if (PartyRows.IsDefault || PartyRows.Length != 3 || PartyRows.Any(row => row >= 8 && row != 255) || !PartyRows.Where(row => row != 255).SequenceEqual(result.Party.Select(member => member.Row)) || result.RewardPolicy != BattleRewardPolicy.NativeCombat || result.Outcome is not (BattleOutcome.Victory or BattleOutcome.Escaped))
        {
            throw new InvalidDataException("Native result experience requires its final physical party mapping and ordinary victory/escape outcome.");
        }
    }

    public BattleExperienceAwards ProjectExperience(NativeCharacterVitals vitals, bool noExperience, GameplayTuning tuning)
    {
        Banks.Validate();
        tuning.Validate();
        if (PartyRows.IsDefault || PartyRows.Length != 3 || PartyRows.Any(row => row >= 8 && row != 255))
        {
            throw new InvalidDataException("Native experience publication requires its physical party mapping.");
        }
        if (noExperience)
        {
            for (int index = 0; index < 38; index++)
            {
                if (Banks.ReadWord(index * 2) != 0)
                {
                    throw new InvalidDataException("Native no-EXP results require their executed48CFF0 bank clear.");
                }
            }
            return new(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>());
        }
        Dictionary<byte, ushort> characters = [];
        for (int index = 0; index < PartyRows.Length; index++)
        {
            byte row = PartyRows[index];
            if (row == 255 || (vitals.Records[row].Status & 5) != 0)
            {
                continue;
            }
            ushort amount = unchecked((ushort)(Banks.ReadWord(index * 2) + Banks.ReadWord(6 + index * 2)));
            characters[row] = ScaleAward(amount, tuning.ExperienceMultiplier);
        }
        Dictionary<byte, ushort> guardians = [];
        for (int id = 0; id < 16; id++)
        {
            if ((vitals.GuardianForces[id].Flags & 1) == 0)
            {
                continue;
            }
            ushort amount = unchecked((ushort)(Banks.ReadWord(12 + 2 * id) + Banks.ReadWord(44 + 2 * id)));
            if (amount != 0)
            {
                guardians[checked((byte)id)] = ScaleAward(amount, tuning.GuardianExperienceMultiplier);
            }
        }
        return new(characters, guardians);
    }

    internal static ushort ScaleAward(ushort value, int multiplier) => checked((ushort)Math.Min(ushort.MaxValue, (long)value * multiplier));
}

public sealed partial record BattleResult
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleNativeExperienceReceipt? NativeExperience { get; init; }
}
