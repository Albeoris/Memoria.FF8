using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public enum Ff7BeatRushSlot : byte
{
    Miss,
    Hit,
    Yeah
}

/// <summary>Projects learned Limit selections and the first Tifa slot result into native attack records.</summary>
public static class Ff7NativeBattleLimits
{
    public static bool IsReady(byte gauge) => gauge == byte.MaxValue;

    public static byte Consume(byte gauge) => IsReady(gauge) ? (byte)0 : throw new InvalidOperationException("FFVII Limit gauge is not full.");

    public static int ReadGaugeDivisor(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        ReadOnlySpan<byte> data = ReadBattleKernel(character, kernel);
        int level = character.NativeRecord[14] - 1;
        int divisor = BinaryPrimitives.ReadInt32LittleEndian(data[(0x28 + character.Id * 56 + level * 4)..]);
        if (divisor <= 0)
        {
            throw new InvalidDataException("FFVII Limit gauge requires a positive native divisor.");
        }
        return divisor;
    }

    public static byte GainFromEnemyDamage(byte gauge, int damage, uint maxHp, uint statuses, int divisor)
    {
        if (maxHp == 0 || divisor <= 0 || damage is < 0 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), "FFVII Limit gauge requires a valid enemy HP damage receipt and player vitals.");
        }
        uint multiplier = (statuses & 0x20) != 0 ? 512u : (statuses & 0x10) != 0 ? 128u : 256u;
        uint gain = ((uint)damage * 300 / maxHp) * multiplier / (uint)divisor;
        return (byte)Math.Min(byte.MaxValue, gauge + gain);
    }

    public static IReadOnlyList<byte> ReadLearnedIds(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        ReadOnlySpan<byte> data = ReadBattleKernel(character, kernel);
        return ReadLearnedIds(character, data, character.NativeRecord[14]);
    }

    public static IReadOnlyList<byte> ReadLearnedIds(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel, int level)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(kernel);
        ReadOnlySpan<byte> data = kernel.Single(section => section.Type == 2).Data.Span;
        if (data.Length != 3988 || character.Id is < 0 or > 8)
        {
            throw new InvalidDataException("FFVII Limit display requires its original character and battle kernel records.");
        }
        return ReadLearnedIds(character, data, level);
    }

    public static int ReadLearnedMask(Ff7CharacterState character, int level)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 4);
        if (character.NativeRecord.Length != 132)
        {
            throw new InvalidDataException("FFVII Limit selection requires the original character record.");
        }

        ushort learned = BinaryPrimitives.ReadUInt16LittleEndian(character.NativeRecord.AsSpan(34));
        return (learned >> ((level - 1) * 3)) & 7;
    }

    private static IReadOnlyList<byte> ReadLearnedIds(Ff7CharacterState character, ReadOnlySpan<byte> data, int level)
    {
        int learned = ReadLearnedMask(character, level);
        int levelStart = 12 + character.Id * 56 + (level - 1) * 3;
        List<byte> result = new(3);
        for (int slot = 0; slot < 3; slot++)
        {
            byte id = data[levelStart + slot];
            if (id != byte.MaxValue && (learned & (1 << slot)) != 0)
            {
                result.Add(id);
            }
        }
        return result.AsReadOnly();
    }

    private static ReadOnlySpan<byte> ReadBattleKernel(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(kernel);
        ReadOnlySpan<byte> native = character.NativeRecord;
        ReadOnlySpan<byte> data = kernel.Single(section => section.Type == 2).Data.Span;
        if (native.Length != 132 || data.Length != 3988 || character.Id is < 0 or > 2 || native[14] is < 1 or > 4)
        {
            throw new InvalidDataException("FFVII opening Limit admission requires its original character and battle kernel records.");
        }
        return data;
    }

    public static Ff7BattleAttack? ReadBeatRush(IReadOnlyList<KernelSection> kernel, Ff7BeatRushSlot result)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        if (!Enum.IsDefined(result))
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }
        if (result == Ff7BeatRushSlot.Miss)
        {
            return null;
        }

        ReadOnlySpan<byte> attacks = kernel.Single(section => section.Type == 1).Data.Span;
        if (attacks.Length != 128 * Ff7BattleAttack.RecordSize)
        {
            throw new InvalidDataException("FFVII Beat Rush requires its original secondary attack record.");
        }
        Ff7BattleAttack attack = Ff7BattleAttack.Read(attacks.Slice(0x62 * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        return result == Ff7BeatRushSlot.Yeah ? attack with { SpecialFlags = (ushort)(attack.SpecialFlags & ~0x2000) } : attack;
    }
}
