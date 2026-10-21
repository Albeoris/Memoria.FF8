using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class NativeTutorialCatalog
{
    /// <summary>Original Junction Auto: 4DC439, priority tables B885C8/B885D8/B885E8 and greedy selectors 4DFDE0/4DFE90.</summary>
    public NativeCharacterVitals AutoJunction(NativeCharacterVitals party, int row, int priority)
    {
        ArgumentNullException.ThrowIfNull(party);
        party.Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)priority, 3u);
        if (kernel.Length < 228 || BinaryPrimitives.ReadInt32LittleEndian(kernel) != 56)
        {
            throw new InvalidDataException("Original Auto junction requires the complete magic kernel section.");
        }
        int start = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(8));
        int end = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(12));
        if (start < 228 || end - start != 57 * 60 || end > kernel.Length)
        {
            throw new InvalidDataException("Original Auto junction requires all fifty-seven sixty-byte magic records.");
        }
        // The authenticated executable fixes these three orders; they are not inferred from host menu positions.
        byte[][] priorities =
        [
            [1, 5, 6, 7, 8, 0, 2, 3, 4, 9, 10, 11, 12],
            [3, 5, 0, 1, 2, 4, 6, 7, 8, 9, 10, 11, 12],
            [0, 2, 4, 6, 10, 12, 1, 3, 5, 7, 8, 9, 11]
        ];
        NativeCharacterVital character = party.Records[row];
        NativeMagicStock[] inventory = party.GetMagicInventory(row).Where(stock => stock.SpellId != 0 && stock.Count != 0 && (!party.MagicSystem.SharedMagicStock || MagicJunctionPolicy.GetOwner(party, stock.SpellId) is not int owner || owner == row)).ToArray();
        HashSet<int> remaining = Enumerable.Range(0, inventory.Length).ToHashSet();
        if (inventory.Any(stock => stock.SpellId >= 57))
        {
            throw new InvalidDataException("Original Auto junction stock has no magic kernel record.");
        }
        byte[] junctions = character.Junction.MagicJunctions.ToArray();
        Array.Clear(junctions, 0, 19);
        foreach (int category in priorities[priority])
        {
            int destination = category < 11 ? category : category == 11 ? 11 : 15;
            int count = category < 11 ? party.JunctionMagicSlotAvailable(row, destination) ? 1 : 0 : Enumerable.Range(destination, 4).Count(slot => party.JunctionMagicSlotAvailable(row, slot));
            for (int repeat = 0; repeat < count; repeat++)
            {
                int selected = -1;
                int maximum = 0;
                for (int index = 0; index < inventory.Length; index++)
                {
                    if (!remaining.Contains(index))
                    {
                        continue;
                    }
                    NativeMagicStock stock = inventory[index];
                    ReadOnlySpan<byte> magic = kernel.AsSpan(start + stock.SpellId * 60, 60);
                    int coefficient = category switch
                    {
                        < 9 => unchecked((sbyte)magic[0x17 + category]),
                        9 => BitOperations.PopCount((uint)magic[0x20]) * magic[0x21],
                        10 => BitOperations.PopCount((uint)BinaryPrimitives.ReadUInt16LittleEndian(magic[0x26..])) * magic[0x24],
                        11 => BitOperations.PopCount((uint)magic[0x22]) * magic[0x23],
                        12 => BitOperations.PopCount((uint)BinaryPrimitives.ReadUInt16LittleEndian(magic[0x28..])) * magic[0x25],
                        _ => throw new InvalidDataException("Original Auto junction priority category is invalid.")
                    };
                    int score = coefficient * unchecked((sbyte)stock.Count);
                    if (score > maximum)
                    {
                        maximum = score;
                        selected = index;
                    }
                }
                if (selected >= 0)
                {
                    remaining.Remove(selected);
                    junctions[destination + repeat] = inventory[selected].SpellId;
                }
            }
        }
        NativeCharacterVital[] records = party.Records.ToArray();
        records[row] = character with { Junction = character.Junction with { MagicJunctions = junctions } };
        NativeCharacterVitals result = (party with { Records = records }).RebuildMagicMenuRow(row);
        return result.RebuildParty();
    }
}
