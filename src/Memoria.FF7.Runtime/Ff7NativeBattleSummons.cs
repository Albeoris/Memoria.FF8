using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleSummon(byte AttackId, Ff7BattleAttack Attack, int InitialUses, byte MpTurboLevel);

public static class Ff7NativeBattleSummons
{
    public static IReadOnlyList<Ff7BattleSummon> Read(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(kernel);
        ReadOnlySpan<byte> characterData = character.NativeRecord;
        ReadOnlySpan<byte> materia = kernel.Single(section => section.Type == 8).Data.Span;
        ReadOnlySpan<byte> attacks = kernel.Single(section => section.Type == 1).Data.Span;
        if (characterData.Length != 132 || materia.Length != 96 * 20 || attacks.Length != 128 * Ff7BattleAttack.RecordSize)
        {
            throw new InvalidDataException("FFVII summons require the original character and kernel records.");
        }

        SortedDictionary<byte, int> initialUses = [];
        for (int offset = 0x40; offset < 0x80; offset += 4)
        {
            uint equipped = BinaryPrimitives.ReadUInt32LittleEndian(characterData[offset..]);
            if ((byte)equipped == byte.MaxValue)
            {
                continue;
            }
            if ((byte)equipped >= materia.Length / 20)
            {
                throw new InvalidDataException("Equipped FFVII materia references an absent original record.");
            }
            ReadOnlySpan<byte> record = materia.Slice((byte)equipped * 20, 20);
            if ((record[13] & 15) != 11)
            {
                continue;
            }
            byte attack = record[14];
            if (attack is < 0x38 or > 0x47)
            {
                throw new InvalidDataException("FFVII summon materia references an absent original attack.");
            }
            int rank = Ff7NativeMateriaInventory.ReadLevel(record, equipped >> 8);
            initialUses[attack] = initialUses.GetValueOrDefault(attack) + rank;
        }

        if (initialUses.Count == 0)
        {
            return [];
        }
        Span<byte> turboLevels = stackalloc byte[128];
        turboLevels.Clear();
        Ff7NativeBattleSpells.ReadMpTurbo(characterData, materia, kernel, turboLevels, 11);
        List<Ff7BattleSummon> result = new(initialUses.Count);
        foreach ((byte id, int uses) in initialUses)
        {
            Ff7BattleAttack attack = Ff7BattleAttack.Read(attacks.Slice(id * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
            byte turboLevel = turboLevels[id];
            result.Add(new(id, Ff7NativeBattleSpells.ApplyMpTurboCost(attack, turboLevel), uses, turboLevel));
        }
        return result.AsReadOnly();
    }

    public static byte SelectChocoMogAttack(Ff7BattleSummon summon, Ff7BattleRng random)
    {
        ArgumentNullException.ThrowIfNull(summon);
        ArgumentNullException.ThrowIfNull(random);
        if (summon is not { AttackId: 0x38, Attack.AdditionalEffect: 2 })
        {
            throw new InvalidDataException("FFVII Choco/Mog requires its original summon and alternate selector.");
        }
        return random.NextByte() < summon.Attack.AdditionalEffectModifier ? (byte)0x60 : summon.AttackId;
    }
}
