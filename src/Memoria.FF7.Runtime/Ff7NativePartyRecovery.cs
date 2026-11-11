using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed record Ff7PartyRecoveryResult(Ff7Progress Progress, bool Underwater);

public static class Ff7NativePartyRecovery
{
    public static Ff7PartyRecoveryResult Publish(Ff7Progress progress, IReadOnlyList<ReadOnlyMemory<byte>> materiaRecords, IReadOnlyList<short> statModifiers, IReadOnlyCollection<int>? guestStoryCharacters = null)
    {
        return ApplyCore(progress, materiaRecords, statModifiers, clearStatus: false, recovery: 0, guestStoryCharacters);
    }

    public static Ff7PartyRecoveryResult AddHitPoints(Ff7Progress progress, int characterId, short amount, IReadOnlyList<ReadOnlyMemory<byte>> materiaRecords, IReadOnlyList<short> statModifiers)
    {
        Ff7PartyRecoveryResult prepared = Publish(progress, materiaRecords, statModifiers);
        Ff7CharacterState[] characters = prepared.Progress.Characters.Select(character =>
        {
            if (character.Id != characterId || !progress.Party.Contains(characterId))
            {
                return character;
            }
            byte[] record = character.NativeRecord.ToArray();
            short current = BinaryPrimitives.ReadInt16LittleEndian(record.AsSpan(44));
            short maximum = BinaryPrimitives.ReadInt16LittleEndian(record.AsSpan(56));
            ushort adjusted = unchecked((ushort)Math.Min(unchecked((short)(current + amount)), maximum));
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), adjusted);
            return character with { HitPoints = adjusted, NativeRecord = record };
        }).ToArray();
        return prepared with { Progress = prepared.Progress with { Characters = characters } };
    }

    public static Ff7PartyRecoveryResult Apply(Ff7Progress progress, IReadOnlyList<ReadOnlyMemory<byte>> materiaRecords, IReadOnlyList<short> statModifiers)
    {
        return ApplyCore(progress, materiaRecords, statModifiers, clearStatus: false);
    }

    public static Ff7PartyRecoveryResult ApplyAll(Ff7Progress progress, IReadOnlyList<ReadOnlyMemory<byte>> materiaRecords, IReadOnlyList<short> statModifiers)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Ff7Progress recovered = progress;
        for (int group = 0; group < 3; group++)
        {
            recovered = ApplyCore(recovered with { Party = [group * 3, group * 3 + 1, group * 3 + 2] }, materiaRecords, statModifiers, clearStatus: true).Progress;
        }
        return ApplyCore(recovered with { Party = progress.Party }, materiaRecords, statModifiers, clearStatus: true);
    }

    private static Ff7PartyRecoveryResult ApplyCore(Ff7Progress progress, IReadOnlyList<ReadOnlyMemory<byte>> materiaRecords, IReadOnlyList<short> statModifiers, bool clearStatus, short recovery = 10000, IReadOnlyCollection<int>? guestStoryCharacters = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(materiaRecords);
        ArgumentNullException.ThrowIfNull(statModifiers);
        if (statModifiers.Count != 17 * 8 || materiaRecords.Any(record => record.Length != 20))
        {
            throw new InvalidDataException("FFVII party recovery requires complete native materia statistics.");
        }
        bool underwater = false;
        Ff7CharacterState[] characters = progress.Characters.Select(character =>
        {
            if (!progress.Party.Contains(character.Id) || guestStoryCharacters?.Contains(character.Id) == true)
            {
                return character;
            }
            byte[] record = character.NativeRecord.ToArray();
            short hpPercent = 0;
            short mpPercent = 0;
            bool swap = false;
            for (int offset = 64; offset < 128; offset += 4)
            {
                uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));
                int id = (byte)encoded;
                if (id == 255)
                {
                    continue;
                }
                if (id >= materiaRecords.Count)
                {
                    throw new InvalidDataException("Equipped FFVII materia references an absent original record.");
                }
                ReadOnlySpan<byte> materia = materiaRecords[id].Span;
                int modifiers = materia[8];
                if (modifiers != 255)
                {
                    if (modifiers >= statModifiers.Count / 8)
                    {
                        throw new NotSupportedException("FFVII materia statistic modifiers exceed the supported native table.");
                    }
                    hpPercent = unchecked((short)(hpPercent + statModifiers[modifiers * 8 + 6]));
                    mpPercent = unchecked((short)(mpPercent + statModifiers[modifiers * 8 + 7]));
                }
                if (materia[13] == 0)
                {
                    swap |= materia[14] == 98;
                    underwater |= materia[14] == 12;
                }
                if (materia[13] is 0x20 or 0x40 && materia[14] is 8 or 9)
                {
                    int level = Ff7NativeMateriaInventory.ReadLevel(materia, encoded >> 8);
                    if (materia[13] == 0x40)
                    {
                        while (level > 0 && materia[14 + level] == 255)
                        {
                            level--;
                        }
                        if (level == 0)
                        {
                            throw new InvalidDataException("FFVII vitality materia has no defined bonus at its current level.");
                        }
                    }
                    if (materia[14] == 8)
                    {
                        hpPercent = unchecked((short)(hpPercent + materia[14 + level]));
                    }
                    else
                    {
                        mpPercent = unchecked((short)(mpPercent + materia[14 + level]));
                    }
                }
            }
            int hp = Maximum(record, 46, hpPercent, 9999);
            int mp = Maximum(record, 50, mpPercent, 999);
            if (swap)
            {
                (hp, mp) = (mp, hp);
            }
            hp = Math.Max(10, hp);
            mp = Math.Max(10, mp);
            ushort currentHp = Recover(BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(44)), hp, recovery);
            ushort currentMp = Recover(BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(48)), mp, recovery);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), currentHp);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(48), currentMp);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(56), checked((ushort)hp));
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(58), checked((ushort)mp));
            if (clearStatus)
            {
                record[31] = 0;
            }
            return character with { HitPoints = currentHp, MagicPoints = currentMp, NativeRecord = record };
        }).ToArray();
        return new(progress with { Characters = characters }, underwater);
    }

    private static int Maximum(ReadOnlySpan<byte> record, int offset, int percent, int limit)
    {
        int basis = BinaryPrimitives.ReadInt16LittleEndian(record[offset..]);
        int adjusted = unchecked((short)(basis + basis * Math.Min(100, percent) / 100));
        return Math.Clamp(adjusted, 0, limit);
    }

    private static ushort Recover(ushort current, int maximum, short amount)
    {
        short clamped = (short)Math.Min(unchecked((short)current), maximum);
        short added = unchecked((short)(clamped + amount));
        return unchecked((ushort)Math.Min(added, maximum));
    }
}
