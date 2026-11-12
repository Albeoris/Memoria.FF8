using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattlePlayerStats(Ff7BattleDamageActor Actor, Ff7BattleDamageAction Attack, byte AttackTargetFlags, ushort AttackElements)
{
    public ushort AttackFlags { get; init; } = ushort.MaxValue;
}

/// <summary>Projects the equipped PC character record into its native battle damage and weapon inputs.</summary>
public static class Ff7NativeBattlePlayerStats
{
    public static Ff7BattlePlayerStats Create(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> materiaModifiers)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(materiaModifiers);
        ReadOnlySpan<byte> record = character.NativeRecord;
        if (record.Length != 132 || kernel.Count <= 8 || kernel[5].Data.Length != 128 * 44 || kernel[6].Data.Length != 32 * 36 || kernel[8].Data.Length != 96 * 20 || materiaModifiers.Count != 17 * 8)
        {
            throw new InvalidDataException("FFVII player damage statistics require the original character, equipment and materia records.");
        }
        if (record[28] >= 128 || record[29] >= 32)
        {
            throw new InvalidDataException("FFVII player damage statistics reference absent equipment.");
        }
        if (record[30] != byte.MaxValue)
        {
            throw new NotSupportedException($"FFVII battle accessory {record[30]} has no supported equipment effects.");
        }

        ReadOnlySpan<byte> weapon = kernel[5].Data.Span.Slice(record[28] * 44, 44);
        ReadOnlySpan<byte> armor = kernel[6].Data.Span.Slice(record[29] * 36, 36);
        Span<byte> equipmentBonuses = stackalloc byte[6];
        AddGearBonuses(equipmentBonuses, weapon[20..]);
        AddGearBonuses(equipmentBonuses, armor[24..]);

        Span<short> equippedBonuses = stackalloc short[6];
        ReadOnlySpan<byte> materia = kernel[8].Data.Span;
        for (int offset = 64; offset < 128; offset += 4)
        {
            int id = record[offset];
            if (id == byte.MaxValue)
            {
                continue;
            }
            if (id >= materia.Length / 20)
            {
                throw new InvalidDataException("Equipped FFVII materia references an absent original record.");
            }
            ReadOnlySpan<byte> equipped = materia.Slice(id * 20, 20);
            if ((equipped[13] & 15) != 9 && ((equipped[13] & 15) != 5 || equipped[14] != 0x51))
            {
                throw new NotSupportedException($"FFVII battle materia {id} has unsupported equipment effects.");
            }
            int row = equipped[8];
            if (row == byte.MaxValue)
            {
                continue;
            }
            if (row >= materiaModifiers.Count / 8)
            {
                throw new InvalidDataException("Equipped FFVII materia references an absent statistic modifier row.");
            }
            for (int stat = 0; stat < equippedBonuses.Length; stat++)
            {
                equippedBonuses[stat] = unchecked((short)(equippedBonuses[stat] + materiaModifiers[row * 8 + stat]));
            }
        }

        Span<int> stats = stackalloc int[6];
        for (int stat = 0; stat < stats.Length; stat++)
        {
            int gearAdjusted = Math.Min(255, record[2 + stat] + record[8 + stat] + equipmentBonuses[stat]);
            int materiaBonus = equippedBonuses[stat] < 0 ? equippedBonuses[stat] : equippedBonuses[stat] & 255;
            stats[stat] = Math.Clamp(gearAdjusted + materiaBonus, 0, 255);
        }

        uint statuses = (uint)(record[31] & 0x30);
        bool backRow = (record[32] & 1) == 0;
        byte attack = (byte)Math.Clamp(stats[0] + weapon[4], 1, 255);
        ushort defense = (ushort)Math.Min(255, stats[1] + armor[2]);
        Ff7BattleDamageActor actor = new(record[1], attack, (byte)stats[2], (byte)stats[4], (byte)stats[5], armor[4], armor[5], defense, (ushort)stats[3], weapon[7], statuses, BackRow: backRow);

        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(weapon[40..]);
        if ((weapon[2] >> 4) is 1 or 3 or 6 or 10 or 11)
        {
            flags = (ushort)(flags & ~4);
        }
        Ff7BattleDamageAction weaponAttack = new(weapon[2], 16, weapon[8], LongRange: (weapon[0] & 0x20) == 0, ForceCritical: (flags & 0x2000) == 0, CanBeBlinded: true);
        ushort elements = BinaryPrimitives.ReadUInt16LittleEndian(weapon[16..]);
        weaponAttack = weaponAttack with { NativePhysicalCategory = (flags & 4) == 0, NativeElements = elements == ushort.MaxValue ? (ushort)0 : elements, IgnoreNativeAffinity = (flags & 0x80) == 0 };
        return new(actor, weaponAttack, weapon[0], elements) { AttackFlags = flags };
    }

    private static void AddGearBonuses(Span<byte> bonuses, ReadOnlySpan<byte> modifiers)
    {
        for (int index = 0; index < 4; index++)
        {
            int stat = modifiers[index];
            if (stat < bonuses.Length)
            {
                bonuses[stat] = unchecked((byte)(bonuses[stat] + modifiers[4 + index]));
            }
        }
    }
}
