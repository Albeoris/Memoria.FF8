using System.Buffers.Binary;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets;

public sealed record BattleWeaponDefinition(int Id, string Name, int CharacterId, int AttackPower, int StrengthBonus, int Tier, int Hit, int Critical, bool Melee);

/// <summary>The original 12-byte weapon records, including the reserved byte at +3.</summary>
internal static class BattleKernelReader
{
    public static IReadOnlyList<BattleWeaponDefinition> Weapons(byte[] kernel)
    {
        if (kernel.Length < 228 || BinaryPrimitives.ReadInt32LittleEndian(kernel) != 56)
        {
            throw new InvalidDataException("Battle weapon metadata requires the original 56-section kernel.bin.");
        }
        int Section(int index) => BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + index * 4));
        int start = Section(4);
        int end = Section(5);
        int text = Section(35);
        int textEnd = Section(36);
        if (start < 228 || end - start != 33 * 12 || end > kernel.Length || text < 228 || textEnd < text || textEnd > kernel.Length)
        {
            throw new InvalidDataException("Original weapon records or strings exceed their kernel sections.");
        }
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        List<BattleWeaponDefinition> weapons = [];
        for (int id = 0; id < 33; id++)
        {
            ReadOnlySpan<byte> record = kernel.AsSpan(start + id * 12, 12);
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(record);
            if (offset >= textEnd - text)
            {
                throw new InvalidDataException("Original weapon name pointer exceeds its text section.");
            }
            ReadOnlySpan<byte> nameBytes = kernel.AsSpan(text + offset, textEnd - text - offset);
            int length = nameBytes.IndexOf((byte)0);
            if (length < 0)
            {
                throw new InvalidDataException("Original weapon name has no terminator.");
            }
            weapons.Add(new BattleWeaponDefinition(id, encoding.GetString(nameBytes[..length]), record[4], record[6], record[8], record[9], record[7], record[10], record[11] != 0));
        }
        return weapons;
    }
}
