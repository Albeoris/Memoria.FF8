using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record BattlePlayerStats(int Strength, int Vitality, int Magic, int Spirit, int Speed, int Luck, int Hit, int Evasion, int[] ElementDefenses, int ElementAttack, int ElementAttackPercent);

/// <summary>Native496440,4962C0,4967C0,4968A0,496930,496960,4969E0 over installed kernel and current saved records.</summary>
internal sealed class BattlePlayerStatReader(byte[] kernel)
{
    public BattlePlayerStats Read(NativeCharacterVitals vitals, int row)
    {
        vitals.Validate();
        NativeCharacterVital character = vitals.Records[row];
        int level = NativeStatDefinitions.Level(character.Experience, vitals.Definitions.Characters[character.CharacterId]);
        int weapon = character.CharacterId >= 8 ? vitals.DreamWeapons[character.CharacterId - 8] : character.Weapon;
        int Quantity(int spell) => spell == 0 ? 0 : character.MagicInventory.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0;
        int Percentage(int selector)
        {
            int percent = 100;
            for (int slot = 0; slot < 4; slot++)
            {
                int ability = (byte)(character.Junction.Abilities >> (8 * slot));
                if (ability is >= 39 and < 58)
                {
                    ReadOnlySpan<byte> passive = Record(13, ability - 39, 8);
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
            ReadOnlySpan<byte> data = Record(6, character.CharacterId, 36).Slice(curve, 4);
            if (data[1] == 0 || data[3] == 0)
            {
                throw new InvalidDataException("Native character stat curve has a zero divisor.");
            }
            int spell = character.Junction.MagicJunctions[junction];
            int junctionBonus = Record(1, spell, 60)[23 + junction] * Quantity(spell) / 100;
            int baseValue = kind is 5 or 8 ? level * data[0] + level / data[1] - level / data[3] + data[2] : (data[2] + level * data[0] / 10 + level / data[1] - level * level / data[3] / 2) / 4;
            int weaponBonus = kind == 1 ? Record(4, weapon, 12)[8] : 0;
            int value = Math.Min(255, character.StatBonuses[bonus] + baseValue + junctionBonus + weaponBonus);
            return Math.Min(255, value * Percentage(kind == 8 ? 8 : kind) / 100);
        }
        int speed = Stat(5, 28, 5, 4);
        int hitSpell = character.Junction.MagicJunctions[7];
        int evadeSpell = character.Junction.MagicJunctions[6];
        int hit = Math.Min(255, Math.Min(255, Record(4, weapon, 12)[7] + Record(1, hitSpell, 60)[30] * Quantity(hitSpell) / 100) * Percentage(7) / 100);
        int evade = Math.Min(255, Math.Min(255, (speed >> 2) + Record(1, evadeSpell, 60)[29] * Quantity(evadeSpell) / 100) * Percentage(6) / 100);
        int[] defenses = new int[8];
        for (int element = 0; element < defenses.Length; element++)
        {
            int total = 800;
            for (int slot = 11; slot < 15; slot++)
            {
                int spell = character.Junction.MagicJunctions[slot];
                ReadOnlySpan<byte> magic = Record(1, spell, 60);
                if ((magic[34] & (1 << element)) != 0)
                {
                    total += magic[35] * Quantity(spell) / 100;
                }
            }
            defenses[element] = Math.Min(1000, total);
        }
        int elementSpell = character.Junction.MagicJunctions[9];
        return new BattlePlayerStats(Stat(1, 12, 1, 0), Stat(2, 16, 2, 1), Stat(3, 20, 3, 2), Stat(4, 24, 4, 3), speed, Stat(8, 32, 8, 5), hit, evade, defenses, Record(1, elementSpell, 60)[32], Record(1, elementSpell, 60)[33] * Quantity(elementSpell) / 100);
    }

    private ReadOnlySpan<byte> Record(int section, int index, int size)
    {
        int start = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 4 * section));
        int end = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(8 + 4 * section));
        if (index < 0 || start < 228 || end > kernel.Length || end < start || index >= (end - start) / size)
        {
            throw new InvalidDataException("Native combat stat record exceeds its installed kernel section.");
        }
        return kernel.AsSpan(start + index * size, size);
    }
}
