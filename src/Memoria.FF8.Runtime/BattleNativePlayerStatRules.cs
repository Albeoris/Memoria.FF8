using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativePlayerStatistics(ushort MaximumHp, int Strength, int Vitality, int Magic, int Spirit, int Speed, int Luck, int Hit, int Evasion, ImmutableArray<int> ElementDefenses, int ElementAttack, int ElementAttackPercent);

/// <summary>Immutable original inputs for495960 and the496xxx player-stat consumers.</summary>
public sealed record BattleNativePlayerStatRules(int CharacterId, byte Level, ImmutableArray<byte> CharacterRecord, ImmutableArray<byte> WeaponRecord, ImmutableArray<ImmutableArray<byte>> MagicRecords, ImmutableArray<ImmutableArray<byte>> PassiveRecords, ImmutableArray<byte> MagicJunctions, uint Abilities, NativeStatDefinitions SavedDefinitions)
{
    public uint? NativeStatusPassiveFlags { get; init; }

    public void Validate()
    {
        if (CharacterId is < 0 or > 10 || Level is < 1 or > 100 || CharacterRecord.IsDefault || CharacterRecord.Length != 36 || WeaponRecord.IsDefault || WeaponRecord.Length != 12 || MagicRecords.IsDefault || MagicRecords.Length != 57 || MagicRecords.Any(record => record.IsDefault || record.Length != 60) || PassiveRecords.IsDefault || PassiveRecords.Length != 19 || PassiveRecords.Any(record => record.IsDefault || record.Length != 8) || MagicJunctions.IsDefault || MagicJunctions.Length != 20 || MagicJunctions.Take(19).Any(spell => spell >= 57))
        {
            throw new InvalidDataException("Native player statistic rules require complete original records and saved junction inputs.");
        }
        SavedDefinitions.Validate();
    }

    public BattleNativePlayerStatistics Project(IReadOnlyList<NativeMagicStock> stocks, BattleNativeCharacterGrowth growth)
    {
        Validate();
        growth.Validate();
        int Quantity(int spell) => spell == 0 ? 0 : stocks.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0;
        int Percentage(int selector)
        {
            int percent = 100;
            for (int slot = 0; slot < 4; slot++)
            {
                int ability = (byte)(Abilities >> (8 * slot));
                if (ability is >= 39 and < 58)
                {
                    ImmutableArray<byte> passive = PassiveRecords[ability - 39];
                    if (passive[5] == selector)
                    {
                        percent += passive[6];
                    }
                }
            }
            return unchecked((byte)percent);
        }
        int Stat(int kind, int curve, int junction, int bonus)
        {
            ReadOnlySpan<byte> data = CharacterRecord.AsSpan().Slice(curve, 4);
            if (data[1] == 0 || data[3] == 0)
            {
                throw new InvalidDataException("Native character stat curve has a zero divisor.");
            }
            int spell = MagicJunctions[junction];
            int junctionBonus = MagicRecords[spell][23 + junction] * Quantity(spell) / 100;
            int baseValue = kind is 5 or 8 ? Level * data[0] + Level / data[1] - Level / data[3] + data[2] : (data[2] + Level * data[0] / 10 + Level / data[1] - Level * Level / data[3] / 2) / 4;
            int weaponBonus = kind == 1 ? WeaponRecord[8] : 0;
            int value = Math.Min(255, growth.StatBonuses[bonus] + baseValue + junctionBonus + weaponBonus);
            return Math.Min(255, value * Percentage(kind == 8 ? 8 : kind) / 100);
        }
        int speed = Stat(5, 28, 5, 4);
        int hitSpell = MagicJunctions[7];
        int evadeSpell = MagicJunctions[6];
        int hit = Math.Min(255, Math.Min(255, WeaponRecord[7] + MagicRecords[hitSpell][30] * Quantity(hitSpell) / 100) * Percentage(7) / 100);
        int evade = Math.Min(255, Math.Min(255, (speed >> 2) + MagicRecords[evadeSpell][29] * Quantity(evadeSpell) / 100) * Percentage(6) / 100);
        int[] defenses = new int[8];
        for (int element = 0; element < defenses.Length; element++)
        {
            int total = 800;
            for (int slot = 11; slot < 15; slot++)
            {
                int spell = MagicJunctions[slot];
                ImmutableArray<byte> magic = MagicRecords[spell];
                if ((magic[34] & (1 << element)) != 0)
                {
                    total += magic[35] * Quantity(spell) / 100;
                }
            }
            defenses[element] = Math.Min(1000, total);
        }
        int elementSpell = MagicJunctions[9];
        byte hpSpell = MagicJunctions[0];
        ushort maximumHp = SavedDefinitions.CharacterMaximum(CharacterId, Level, growth.HitPointBonus, hpSpell, Quantity(hpSpell), Abilities);
        return new BattleNativePlayerStatistics(maximumHp, Stat(1, 12, 1, 0), Stat(2, 16, 2, 1), Stat(3, 20, 3, 2), Stat(4, 24, 4, 3), speed, Stat(8, 32, 8, 5), hit, evade, defenses.ToImmutableArray(), MagicRecords[elementSpell][32], MagicRecords[elementSpell][33] * Quantity(elementSpell) / 100);
    }

    public BattleNativeAction ProjectAttack(BattleNativeAction source, IReadOnlyList<NativeMagicStock> stocks, BattleNativePlayerStatistics statistics)
    {
        int spellId = MagicJunctions[10];
        int quantity = spellId == 0 ? 0 : stocks.FirstOrDefault(stock => stock.SpellId == spellId)?.Count ?? 0;
        ImmutableArray<byte> spell = MagicRecords[spellId];
        return source with { Hit = checked((byte)statistics.Hit), Element = checked((byte)statistics.ElementAttack), ElementPercent = checked((byte)statistics.ElementAttackPercent), StatusAttack = checked((byte)(100 + spell[36] * quantity / 100)) };
    }

    public ImmutableArray<byte> ProjectStatusResistances(IReadOnlyList<NativeMagicStock> stocks)
    {
        Validate();
        uint passiveFlags = NativeStatusPassiveFlags ?? throw new NotSupportedException("Native player status projection requires its original equipped character-ability flags.");
        byte[] defense = new byte[13];
        for (int bit = 0; bit < defense.Length; bit++)
        {
            int total = 100;
            for (int slot = 15; slot < 19; slot++)
            {
                int spellId = MagicJunctions[slot];
                ImmutableArray<byte> spell = MagicRecords[spellId];
                int enabled = spell[40] | spell[41] << 8;
                int quantity = spellId == 0 ? 0 : stocks.FirstOrDefault(stock => stock.SpellId == spellId)?.Count ?? 0;
                if ((enabled & (1 << bit)) != 0)
                {
                    total += spell[37] * quantity / 100;
                }
            }
            defense[bit] = (byte)Math.Min(200, total);
        }
        byte[] resistance = Enumerable.Repeat((byte)100, 40).ToArray();
        int[] indices = [0, 1, 2, 3, 4, 5, 6, 8, 10, 11, 17, 22, 23];
        for (int index = 0; index < indices.Length; index++)
        {
            resistance[indices[index]] = defense[index];
        }
        resistance[20] = defense[2];
        if ((passiveFlags & 0x8000) != 0)
        {
            resistance[10] = 200;
            resistance[11] = 200;
        }
        if ((passiveFlags & 0x80000) != 0)
        {
            foreach (int index in new[] { 0, 1, 2, 3, 4, 5, 6, 8, 10, 11, 17, 18, 20, 22, 23, 24, 32 })
            {
                resistance[index] = 200;
            }
        }
        return resistance.ToImmutableArray();
    }
}
