using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleSpell(byte AttackId, Ff7BattleAttack Attack);

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
        List<Ff7BattleSpell> spells = new(available.Count);
        foreach (byte spell in available)
        {
            spells.Add(new(spell, Ff7BattleAttack.Read(attacks.Slice(spell * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize))));
        }
        return spells.AsReadOnly();
    }
}
