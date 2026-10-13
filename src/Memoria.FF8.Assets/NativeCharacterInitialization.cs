using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeCharacterInitialization
{
    public static NativeCharacterVitals Read(byte[] initial, byte[] kernel)
    {
        if (initial.Length != 2812 || kernel.Length < 228 || BinaryPrimitives.ReadUInt32LittleEndian(kernel) != 56)
        {
            throw new InvalidDataException("Original New Game template/kernel structure is unsupported.");
        }
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(28)));
        int end = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(32)));
        if (start < 228 || end > kernel.Length || end - start != 11 * 36)
        {
            throw new InvalidDataException("Kernel character section6 is invalid.");
        }
        NativeCharacterVital[] records = new NativeCharacterVital[8];
        for (int index = 0; index < records.Length; index++)
        {
            ReadOnlySpan<byte> record = initial.AsSpan(1088 + index * 152, 152);
            byte character = record[8];
            if (character != index || record[92] != 0 || record.Slice(84, 4).ContainsAnyExcept((byte)0))
            {
                throw new NotSupportedException("New Game character mapping or HP junction/ability modifiers need their original stat contract.");
            }
            ReadOnlySpan<byte> stats = kernel.AsSpan(start + character * 36, 36);
            uint experience = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
            int level = 1;
            int accumulated = 10 * stats[6];
            int fractional = stats[7];
            while (level < 100 && experience >= accumulated + level * fractional / 256)
            {
                level++;
                fractional += stats[7];
                accumulated += 10 * stats[6];
            }
            if (stats[9] == 0)
            {
                throw new InvalidDataException("Native initial HP formula divides by zero.");
            }
            int maximum = BinaryPrimitives.ReadUInt16LittleEndian(record[2..]) + stats[10] + level * stats[8] - 10 * level * level / stats[9];
            if (maximum <= 0 || maximum > short.MaxValue)
            {
                throw new NotSupportedException("Native initial HP maximum is not proved strictly positive.");
            }
            ushort hp = BinaryPrimitives.ReadUInt16LittleEndian(record);
            NativeCharacterJunction junction = new(BinaryPrimitives.ReadUInt16LittleEndian(record[88..]), BinaryPrimitives.ReadUInt32LittleEndian(record[80..]), BinaryPrimitives.ReadUInt32LittleEndian(record[84..]), record.Slice(92, 20).ToArray(), (ushort)Math.Min(maximum, 9999));
            records[index] = new NativeCharacterVital(character, new NativeHpWord(hp, hp), BinaryPrimitives.ReadUInt16LittleEndian(record[150..]), junction);
        }
        NativeCharacterVitals state = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = Convert.ToHexString(SHA256.HashData(initial)),
            KernelSha256 = Convert.ToHexString(SHA256.HashData(kernel)),
            Records = records,
            CombatParty = initial.AsSpan(2724, 3).ToArray(),
            // Native56D9F0 zeros1CFE978; the template ends before this global junction flag.
            JunctionResetEnabled = false
        };
        state = state.RebuildParty();
        byte[] party = state.CombatParty.ToArray();
        for (int slot = 0; slot < party.Length; slot++)
        {
            // Native New Game menu4E445D..447B removes combat records without membership bit1.
            if (party[slot] != 255 && (initial[1088 + party[slot] * 152 + 148] & 1) == 0)
            {
                party[slot] = 255;
            }
        }
        if ((initial[1088 + 148] & 1) == 0)
        {
            throw new NotSupportedException("Native New Game field-party Squall lacks its original membership flag.");
        }
        return state with { CombatParty = party };
    }
}
