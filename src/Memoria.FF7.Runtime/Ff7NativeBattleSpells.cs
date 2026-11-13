using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleSpell(byte AttackId, Ff7BattleAttack Attack, byte MpTurboLevel = 0);

/// <summary>Projects the spell IDs enabled by the character's equipped native magic materia.</summary>
public static class Ff7NativeBattleSpells
{
    public static IReadOnlyList<Ff7BattleSpell> Read(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(kernel);
        ReadOnlySpan<byte> materia = kernel.Single(section => section.Type == 8).Data.Span;
        ReadOnlySpan<byte> attacks = kernel.Single(section => section.Type == 1).Data.Span;
        if (character.NativeRecord.Length != 132 || materia.Length != 96 * 20 || attacks.Length != 128 * Ff7BattleAttack.RecordSize)
        {
            throw new InvalidDataException("FFVII battle spell admission requires original character, materia and attack records.");
        }
        SortedSet<byte> available = [];
        for (int offset = 64; offset < 128; offset += 4)
        {
            uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(character.NativeRecord.AsSpan(offset));
            int id = (byte)encoded;
            if (id == byte.MaxValue)
            {
                continue;
            }
            if (id >= materia.Length / 20)
            {
                throw new InvalidDataException("Equipped FFVII materia references an absent original record.");
            }
            ReadOnlySpan<byte> equipped = materia.Slice(id * 20, 20);
            if ((equipped[13] & 0x0F) != 9)
            {
                continue;
            }
            int level = Ff7NativeMateriaInventory.ReadLevel(equipped, encoded >> 8);
            foreach (byte spell in equipped.Slice(14, level))
            {
                if (spell < 56)
                {
                    available.Add(spell);
                }
            }
        }
        if (available.Count == 0)
        {
            return [];
        }

        ReadOnlySpan<byte> record = character.NativeRecord;
        ReadOnlySpan<byte> weaponSlots = ReadGearSlots(kernel, 5, 128, 44, 28, record[28]);
        ReadOnlySpan<byte> armorSlots = ReadGearSlots(kernel, 6, 32, 36, 9, record[29]);
        Span<byte> turboLevels = stackalloc byte[56];
        turboLevels.Clear();
        ReadMpTurbo(weaponSlots, record.Slice(64, 32), materia, turboLevels);
        ReadMpTurbo(armorSlots, record.Slice(96, 32), materia, turboLevels);

        List<Ff7BattleSpell> spells = new(available.Count);
        foreach (byte spell in available)
        {
            Ff7BattleAttack attack = Ff7BattleAttack.Read(attacks.Slice(spell * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
            byte turboLevel = turboLevels[spell];
            if (turboLevel != 0)
            {
                int cost = attack.CastingCost;
                attack = attack with { CastingCost = (ushort)Math.Min(255, cost + cost * turboLevel / 10 + 1) };
            }
            spells.Add(new(spell, attack, turboLevel));
        }
        return spells.AsReadOnly();
    }

    private static void ReadMpTurbo(ReadOnlySpan<byte> slots, ReadOnlySpan<byte> equipped, ReadOnlySpan<byte> materia, Span<byte> levels)
    {
        for (int slot = 0; slot < slots.Length; slot += 2)
        {
            if ((slots[slot], slots[slot + 1]) is not ((2, 3) or (6, 7)))
            {
                continue;
            }
            uint left = BinaryPrimitives.ReadUInt32LittleEndian(equipped[(slot * 4)..]);
            uint right = BinaryPrimitives.ReadUInt32LittleEndian(equipped[((slot + 1) * 4)..]);
            AddMpTurbo(left, right, materia, levels);
            AddMpTurbo(right, left, materia, levels);
        }
    }

    private static void AddMpTurbo(uint support, uint magic, ReadOnlySpan<byte> materia, Span<byte> levels)
    {
        if ((byte)support == byte.MaxValue || (byte)magic == byte.MaxValue)
        {
            return;
        }

        ReadOnlySpan<byte> supportRecord = materia.Slice((byte)support * 20, 20);
        ReadOnlySpan<byte> magicRecord = materia.Slice((byte)magic * 20, 20);
        if ((supportRecord[13] & 15) != 5 || supportRecord[14] != 0x58 || (magicRecord[13] & 15) != 9)
        {
            return;
        }

        int supportLevel = Ff7NativeMateriaInventory.ReadLevel(supportRecord, support >> 8);
        int magicLevel = Ff7NativeMateriaInventory.ReadLevel(magicRecord, magic >> 8);
        foreach (byte spell in magicRecord.Slice(14, magicLevel))
        {
            if (spell < levels.Length)
            {
                levels[spell] = (byte)Math.Min(5, levels[spell] + supportLevel);
            }
        }
    }

    private static ReadOnlySpan<byte> ReadGearSlots(IReadOnlyList<KernelSection> kernel, ushort type, int count, int stride, int offset, int id)
    {
        ReadOnlySpan<byte> data = kernel.Single(section => section.Type == type).Data.Span;
        if (id >= count || data.Length != count * stride)
        {
            throw new InvalidDataException("FFVII battle spell admission requires original weapon and armor records.");
        }
        return data.Slice(id * stride + offset, 8);
    }
}
