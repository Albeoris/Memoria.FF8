using System.Buffers.Binary;
using System.Collections.ObjectModel;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Reads the initial per-spell battle uses granted by linked All materia.</summary>
public static class Ff7NativeBattleAll
{
    public static IReadOnlyDictionary<byte, int> Read(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(kernel);
        ReadOnlySpan<byte> weapons = kernel.Single(section => section.Type == 5).Data.Span;
        ReadOnlySpan<byte> armor = kernel.Single(section => section.Type == 6).Data.Span;
        ReadOnlySpan<byte> materia = kernel.Single(section => section.Type == 8).Data.Span;
        ReadOnlySpan<byte> attacks = kernel.Single(section => section.Type == 1).Data.Span;
        ReadOnlySpan<byte> record = character.NativeRecord;
        if (record.Length != 132 || weapons.Length != 128 * 44 || armor.Length != 32 * 36 || materia.Length != 96 * 20 || attacks.Length != 128 * Ff7BattleAttack.RecordSize)
        {
            throw new InvalidDataException("FFVII All materia admission requires original character and kernel records.");
        }
        int weaponId = record[0x1C];
        int armorId = record[0x1D];
        if (weaponId >= 128 || armorId >= 32)
        {
            throw new InvalidDataException("Equipped FFVII gear references an absent original record.");
        }

        Dictionary<byte, int> uses = [];
        AddPairs(weapons.Slice(weaponId * 44 + 0x1C, 8), record.Slice(0x40, 32), materia, attacks, uses);
        AddPairs(armor.Slice(armorId * 36 + 0x09, 8), record.Slice(0x60, 32), materia, attacks, uses);
        return new ReadOnlyDictionary<byte, int>(uses);
    }

    private static void AddPairs(ReadOnlySpan<byte> slots, ReadOnlySpan<byte> equipped, ReadOnlySpan<byte> materia, ReadOnlySpan<byte> attacks, Dictionary<byte, int> uses)
    {
        for (int slot = 0; slot < 8; slot += 2)
        {
            if ((slots[slot], slots[slot + 1]) is not ((2, 3) or (6, 7)))
            {
                continue;
            }
            uint left = BinaryPrimitives.ReadUInt32LittleEndian(equipped[(slot * 4)..]);
            uint right = BinaryPrimitives.ReadUInt32LittleEndian(equipped[((slot + 1) * 4)..]);
            if ((byte)left == byte.MaxValue || (byte)right == byte.MaxValue)
            {
                continue;
            }
            if ((byte)left >= materia.Length / 20 || (byte)right >= materia.Length / 20)
            {
                throw new InvalidDataException("Linked FFVII materia references an absent original record.");
            }

            ReadOnlySpan<byte> leftMateria = materia.Slice((byte)left * 20, 20);
            ReadOnlySpan<byte> rightMateria = materia.Slice((byte)right * 20, 20);
            AddPair(leftMateria, left >> 8, rightMateria, right >> 8, attacks, uses);
            AddPair(rightMateria, right >> 8, leftMateria, left >> 8, attacks, uses);
        }
    }

    private static void AddPair(ReadOnlySpan<byte> support, uint supportAp, ReadOnlySpan<byte> magic, uint magicAp, ReadOnlySpan<byte> attacks, Dictionary<byte, int> uses)
    {
        if ((support[13] & 0x0F) != 5 || support[14] != 0x51 || (magic[13] & 0x0F) != 9)
        {
            return;
        }
        int supportLevel = Ff7NativeMateriaInventory.ReadLevel(support, supportAp);
        int magicLevel = Ff7NativeMateriaInventory.ReadLevel(magic, magicAp);
        foreach (byte spell in magic.Slice(14, magicLevel))
        {
            if (spell < 56 && (attacks[spell * Ff7BattleAttack.RecordSize + 0x0C] & 8) != 0)
            {
                uses[spell] = uses.GetValueOrDefault(spell) + supportLevel;
            }
        }
    }
}
