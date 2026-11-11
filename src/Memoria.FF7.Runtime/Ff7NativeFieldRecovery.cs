using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Resolves single-target recovery from the native field Item and Magic menus.</summary>
public static class Ff7NativeFieldRecovery
{
    public static bool SupportsItem(int itemId, IReadOnlyList<KernelSection> kernel)
    {
        if (itemId is < 0 or > 7 or 6)
        {
            return false;
        }

        ReadOnlySpan<byte> items = ReadSection(kernel, 4, 128 * Ff7BattleAttack.RecordSize);
        return (items[itemId * Ff7BattleAttack.RecordSize + 10] & 4) == 0;
    }

    public static IReadOnlyList<Ff7BattleSpell> ReadMagic(Ff7CharacterState caster, IReadOnlyList<KernelSection> kernel)
    {
        IReadOnlyList<Ff7BattleSpell> spells = Ff7NativeBattleSpells.Read(caster, kernel);
        if (spells.Count == 0)
        {
            return spells;
        }

        ReadOnlySpan<byte> record = caster.NativeRecord;
        ReadOnlySpan<byte> weapon = ReadGear(kernel, 5, 128, 44, record[28]);
        ReadOnlySpan<byte> armor = ReadGear(kernel, 6, 32, 36, record[29]);
        ReadOnlySpan<byte> materia = ReadSection(kernel, 8, 96 * 20);
        Span<int> turboLevels = stackalloc int[56];
        turboLevels.Clear();
        ReadMpTurbo(weapon.Slice(28, 8), record.Slice(64, 32), materia, turboLevels);
        ReadMpTurbo(armor.Slice(9, 8), record.Slice(96, 32), materia, turboLevels);

        List<Ff7BattleSpell> projected = new(spells.Count);
        foreach (Ff7BattleSpell spell in spells)
        {
            int level = turboLevels[spell.AttackId];
            int cost = spell.Attack.CastingCost;
            int adjusted = level == 0 ? cost : Math.Min(255, cost + cost * level / 10 + 1);
            projected.Add(spell with { Attack = spell.Attack with { CastingCost = checked((ushort)adjusted) } });
        }
        return projected.AsReadOnly();
    }

    public static bool SupportsMagic(int spellId) => spellId is 0 or 1 or 2 or 7 or 8 or 51;

    public static bool CanUseItem(Ff7CharacterState target, int itemId)
    {
        (int hp, int mp) = ItemVitals(target, itemId);
        return hp != target.HitPoints || mp != target.MagicPoints;
    }

    public static bool CanCastMagic(Ff7CharacterState target, int spellId)
    {
        if (!SupportsMagic(spellId) || Maximum(target, 56) is 0 or ushort.MaxValue)
        {
            return false;
        }

        return spellId is 7 or 8
            ? target.HitPoints == 0
            : target.HitPoints > 0 && target.HitPoints < Maximum(target, 56);
    }

    public static bool TryUseItem(Ff7Progress progress, int itemId, int targetId, IReadOnlyList<KernelSection> kernel, IReadOnlyCollection<int>? guests, out Ff7Progress result)
    {
        result = progress;
        if (!SupportsItem(itemId, kernel) || Resident(progress, targetId, guests) is not { } target)
        {
            return false;
        }

        (int hp, int mp) = ItemVitals(target, itemId);
        if (hp == target.HitPoints && mp == target.MagicPoints || !Ff7NativeBattleActionResolver.TrySpendItem(progress.Inventory, (ushort)itemId, out Ff7InventorySlot[] stock))
        {
            return false;
        }

        result = Update(progress with { Inventory = stock }, WithVitals(target, hp, mp));
        return true;
    }

    public static bool TryCastMagic(Ff7Progress progress, int casterId, int spellId, int targetId, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> statModifiers, IReadOnlyCollection<int>? guests, out Ff7Progress result)
    {
        result = progress;
        if (!SupportsMagic(spellId) || Resident(progress, casterId, guests) is not { } caster || Resident(progress, targetId, guests) is not { } target || !CanCastMagic(target, spellId))
        {
            return false;
        }

        Ff7BattleSpell? spell = ReadMagic(caster, kernel).SingleOrDefault(candidate => candidate.AttackId == spellId);
        if (spell is null || caster.MagicPoints < spell.Attack.CastingCost)
        {
            return false;
        }

        int maxHp = Maximum(target, 56);
        int hp = spellId switch
        {
            7 => maxHp / 4,
            8 => maxHp,
            51 => maxHp,
            _ => Math.Min(maxHp, target.HitPoints + CureAmount(caster, spellId, kernel, statModifiers))
        };
        int remaining = caster.MagicPoints - spell.Attack.CastingCost;
        result = casterId == targetId
            ? Update(progress, WithVitals(target, hp, remaining))
            : Update(progress, WithVitals(target, hp, target.MagicPoints), WithVitals(caster, caster.HitPoints, remaining));
        return true;
    }

    internal static Ff7CharacterState? Resident(Ff7Progress progress, int characterId, IReadOnlyCollection<int>? guests)
    {
        return (uint)characterId <= 8 && progress.Party.Contains(characterId) && guests?.Contains(characterId) != true
            ? progress.Characters.Single(character => character.Id == characterId)
            : null;
    }

    private static (int Hp, int Mp) ItemVitals(Ff7CharacterState target, int itemId)
    {
        int hp = target.HitPoints;
        int mp = target.MagicPoints;
        int maxHp = Maximum(target, 56);
        int maxMp = Maximum(target, 58);
        if (hp == 0)
        {
            return itemId == 7 && maxHp is > 0 and < ushort.MaxValue ? (maxHp / 4, mp) : (hp, mp);
        }

        if (maxHp is > 0 and < ushort.MaxValue && hp < maxHp)
        {
            hp = itemId switch
            {
                0 => Math.Min(maxHp, hp + 100),
                1 => Math.Min(maxHp, hp + 500),
                2 or 5 => maxHp,
                _ => hp
            };
        }

        if (maxMp is > 0 and < ushort.MaxValue && mp < maxMp)
        {
            mp = itemId switch
            {
                3 => Math.Min(maxMp, mp + 100),
                4 or 5 => maxMp,
                _ => mp
            };
        }

        return (hp, mp);
    }

    private static int CureAmount(Ff7CharacterState caster, int spellId, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers)
    {
        ReadOnlySpan<byte> record = caster.NativeRecord;
        ReadOnlySpan<byte> weapon = ReadGear(kernel, 5, 128, 44, record[28]);
        ReadOnlySpan<byte> armor = ReadGear(kernel, 6, 32, 36, record[29]);
        ReadOnlySpan<byte> materia = ReadSection(kernel, 8, 96 * 20);
        int gearBonus = MagicBonus(weapon.Slice(20, 8)) + MagicBonus(armor.Slice(24, 8));
        if (record[30] != byte.MaxValue)
        {
            ReadOnlySpan<byte> accessory = ReadGear(kernel, 7, 32, 16, record[30]);
            gearBonus += MagicBonus(accessory[..4]);
        }

        int gearMagic = Math.Min(255, record[4] + record[10] + (byte)gearBonus + (record[30] == 25 ? 15 : 0));
        short materiaBonus = 0;
        int magicPercent = 0;
        if (modifiers.Count != 17 * 8)
        {
            throw new InvalidDataException("FFVII field recovery requires the original materia stat modifiers.");
        }

        for (int offset = 64; offset < 128; offset += 4)
        {
            uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(record[offset..]);
            if ((byte)encoded == byte.MaxValue)
            {
                continue;
            }

            ReadOnlySpan<byte> equipped = ReadMateria(materia, (byte)encoded);
            int row = equipped[8];
            if (row != byte.MaxValue)
            {
                if (row >= modifiers.Count / 8)
                {
                    throw new InvalidDataException("Equipped FFVII materia references an absent stat modifier row.");
                }
                materiaBonus = unchecked((short)(materiaBonus + modifiers[row * 8 + 2]));
            }

            if (equipped[13] == 0x20 && equipped[14] == 2)
            {
                int level = Ff7NativeMateriaInventory.ReadLevel(equipped, encoded >> 8);
                magicPercent += equipped[14 + level];
            }
        }

        int magic = Math.Clamp(gearMagic + gearMagic * Math.Min(100, magicPercent) / 100 + (materiaBonus < 0 ? materiaBonus : (byte)materiaBonus), 0, 255);
        int offsetHp = spellId switch
        {
            0 => 110,
            1 => 660,
            _ => 1760
        };
        return 6 * (magic + record[1]) + offsetHp;
    }

    private static int MagicBonus(ReadOnlySpan<byte> stats)
    {
        int count = stats.Length / 2;
        int bonus = 0;
        for (int index = 0; index < count; index++)
        {
            if (stats[index] == 2)
            {
                bonus += stats[count + index];
            }
        }
        return bonus;
    }

    private static void ReadMpTurbo(ReadOnlySpan<byte> slots, ReadOnlySpan<byte> equipped, ReadOnlySpan<byte> materia, Span<int> levels)
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

    private static void AddMpTurbo(uint support, uint magic, ReadOnlySpan<byte> materia, Span<int> levels)
    {
        if ((byte)support == byte.MaxValue || (byte)magic == byte.MaxValue)
        {
            return;
        }

        ReadOnlySpan<byte> supportRecord = ReadMateria(materia, (byte)support);
        ReadOnlySpan<byte> magicRecord = ReadMateria(materia, (byte)magic);
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
                levels[spell] = Math.Min(5, levels[spell] + supportLevel);
            }
        }
    }

    private static ReadOnlySpan<byte> ReadMateria(ReadOnlySpan<byte> materia, int id)
    {
        if (id >= materia.Length / 20)
        {
            throw new InvalidDataException("Equipped FFVII materia references an absent original record.");
        }
        return materia.Slice(id * 20, 20);
    }

    private static ReadOnlySpan<byte> ReadGear(IReadOnlyList<KernelSection> kernel, ushort section, int count, int stride, int id)
    {
        if (id >= count)
        {
            throw new InvalidDataException("Equipped FFVII gear references an absent original record.");
        }
        return ReadSection(kernel, section, count * stride).Slice(id * stride, stride);
    }

    private static ReadOnlySpan<byte> ReadSection(IReadOnlyList<KernelSection> kernel, ushort type, int length)
    {
        ReadOnlySpan<byte> data = kernel.Single(section => section.Type == type).Data.Span;
        if (data.Length != length)
        {
            throw new InvalidDataException("FFVII field recovery requires the original kernel records.");
        }
        return data;
    }

    private static int Maximum(Ff7CharacterState character, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(character.NativeRecord.AsSpan(offset));

    private static Ff7CharacterState WithVitals(Ff7CharacterState character, int hp, int mp)
    {
        byte[] record = character.NativeRecord.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), checked((ushort)hp));
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(48), checked((ushort)mp));
        return character with { HitPoints = hp, MagicPoints = mp, NativeRecord = record };
    }

    private static Ff7Progress Update(Ff7Progress progress, Ff7CharacterState target, Ff7CharacterState? caster = null)
    {
        return progress with
        {
            Characters = progress.Characters.Select(character => character.Id == target.Id ? target : caster is not null && character.Id == caster.Id ? caster : character).ToArray()
        };
    }
}
