using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The contiguous experience and AP WORDs at 1CFF574..1CFF5DF.</summary>
public sealed record NativeBattleExperienceBanks([property: JsonRequired] ImmutableArray<ushort?> Words)
{
    public static NativeBattleExperienceBanks Initialize48D020() => new(Enumerable.Repeat<ushort?>(0, 54).ToImmutableArray());

    public void Validate()
    {
        if (Words.IsDefault || Words.Length != 54)
        {
            throw new InvalidDataException("Native reward memory requires all 54 physical experience/AP WORD owners.");
        }
    }

    public ushort ReadWord(int offset)
    {
        Validate();
        ValidateOffset(offset);
        return Words[offset / 2] ?? throw new NotSupportedException("The requested native reward WORD has no established physical owner.");
    }

    public NativeBattleExperienceBanks Accrue494AF0(NativeBattleExperienceInput input)
    {
        Validate();
        input.Validate();
        NativeBattleExperienceBanks result = this;
        if (input.Command == 254)
        {
            result = result.AddCapped(44 + 2 * input.GuardianForceId!.Value, Scale(input.PersonalBasis, input.EnemyLevel, input.RecipientLevel));
        }
        else if (input.Command is not (0 or 7 or 29 or 245 or 246))
        {
            result = result.AddCapped(6 + 2 * input.ActorIndex, Scale(input.PersonalBasis, input.EnemyLevel, input.RecipientLevel));
        }
        if (input.Command is not (7 or 29))
        {
            result = result.AddCapped(0, PartialShared(input.SharedBasis, input.EnemyLevel, input.AveragePartyLevel, input.MaximumHp, input.CurrentHp));
        }
        return result.WriteWord(76, unchecked((ushort)(result.ReadWord(76) + input.AbilityPoints)));
    }

    public NativeBattleExperienceBanks Distribute494D40(IReadOnlyList<NativeBattleExperiencePartySlot> party, IReadOnlyList<ushort> guardianHp, IReadOnlyList<NativeBattleExperienceSurvivor> survivors, bool noExperience)
    {
        Validate();
        if (party.Count != 3 || guardianHp.Count != 16)
        {
            throw new ArgumentException("Native reward distribution requires three physical party slots and sixteen GF HP WORDs.");
        }
        NativeBattleExperienceBanks result = this;
        foreach (NativeBattleExperienceSurvivor enemy in survivors)
        {
            enemy.Validate();
            if ((enemy.SavedStatus & 1) == 0)
            {
                result = result.AddCapped(0, PartialShared(enemy.Basis, enemy.Level, enemy.AveragePartyLevel, enemy.MaximumHp, enemy.CurrentHp));
            }
        }
        ushort shared = result.ReadWord(0);
        for (int index = 0; index < 3; index++)
        {
            NativeBattleExperiencePartySlot slot = party[index];
            if (slot.SavedStatus is not ushort status)
            {
                if (slot.Active)
                {
                    throw new NotSupportedException("Native active reward recipients require their actual saved status WORD.");
                }
                // An absent actor's retained status is not produced by its inactive allocation branch.
                result = result.WriteWord(2 * index, null).WriteWord(6 + 2 * index, result.ReadWord(6 + 2 * index) == 0 ? (ushort)0 : null);
            }
            else if ((status & 5) != 0)
            {
                result = result.WriteWord(2 * index, 0).WriteWord(6 + 2 * index, 0);
            }
            else
            {
                result = result.WriteWord(2 * index, shared);
            }
        }
        ushort ap = result.ReadWord(76);
        result = result.WriteWord(76, 0);
        foreach (NativeBattleExperiencePartySlot slot in party)
        {
            if (!slot.Active || slot.SavedStatus is not ushort status || (status & 5) == 5)
            {
                continue;
            }
            int count = System.Numerics.BitOperations.PopCount((uint)slot.GuardianMask);
            for (int id = 0; id < 16; id++)
            {
                if ((slot.GuardianMask & (1 << id)) == 0)
                {
                    continue;
                }
                if (guardianHp[id] != 0)
                {
                    result = result.WriteWord(12 + 2 * id, (ushort)(shared / count)).WriteWord(76 + 2 * id, ap);
                }
                else
                {
                    result = result.WriteWord(44 + 2 * id, 0);
                }
            }
        }
        return noExperience ? result.ClearExperience48CFF0() : result;
    }

    public NativeBattleExperienceBanks ClearExperience48CFF0()
    {
        Validate();
        ImmutableArray<ushort?>.Builder words = Words.ToBuilder();
        for (int index = 0; index < 38; index++)
        {
            words[index] = 0;
        }
        return this with { Words = words.ToImmutable() };
    }

    internal static ushort Scale(ushort basis, int enemyLevel, int recipientLevel)
    {
        if (basis == 0)
        {
            return 0;
        }
        int value = 5 * basis * enemyLevel / recipientLevel - basis;
        return value < 1 ? (ushort)1 : Math.Min(unchecked((ushort)value), (ushort)60000);
    }

    internal static ushort PartialShared(ushort basis, int enemyLevel, int recipientLevel, int maximumHp, int currentHp)
    {
        if (basis == 0 || maximumHp == currentHp)
        {
            return 0;
        }
        int scaled = 5 * basis * enemyLevel / recipientLevel - basis;
        int value = unchecked((maximumHp - currentHp) * scaled) / maximumHp;
        return value < 1 ? (ushort)1 : Math.Min(unchecked((ushort)value), (ushort)60000);
    }

    private NativeBattleExperienceBanks AddCapped(int offset, ushort value) => WriteWord(offset, Math.Min(unchecked((ushort)(ReadWord(offset) + value)), (ushort)60000));

    private NativeBattleExperienceBanks WriteWord(int offset, ushort? value) => this with { Words = Words.SetItem(offset / 2, value) };

    private static void ValidateOffset(int offset)
    {
        if (offset is < 0 or > 106 || (offset & 1) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }
}

public sealed record NativeBattleExperienceInput(byte ActorIndex, byte Command, byte? GuardianForceId, byte EnemyLevel, byte RecipientLevel, byte AveragePartyLevel, ushort PersonalBasis, ushort SharedBasis, int MaximumHp, int CurrentHp, byte AbilityPoints)
{
    public void Validate()
    {
        if (ActorIndex >= 7 || GuardianForceId >= 16 || (Command == 254) != (GuardianForceId is not null) || EnemyLevel is < 1 or > 100 || RecipientLevel is < 1 or > 100 || AveragePartyLevel is < 1 or > 100 || MaximumHp <= 0 || CurrentHp < 0 || CurrentHp > MaximumHp)
        {
            throw new InvalidDataException("Native experience accrual requires its actual command, recipient, level and HP operands.");
        }
    }
}

public sealed record NativeBattleExperiencePartySlot(bool Active, ushort? SavedStatus, ushort GuardianMask);

public sealed record NativeBattleExperienceSurvivor(ushort SavedStatus, ushort Basis, byte Level, byte AveragePartyLevel, int MaximumHp, int CurrentHp)
{
    public void Validate()
    {
        if (Level is < 1 or > 100 || AveragePartyLevel is < 1 or > 100 || MaximumHp <= 0 || CurrentHp < 0 || CurrentHp > MaximumHp)
        {
            throw new InvalidDataException("Native surviving-enemy rewards require current level and HP operands.");
        }
    }
}
