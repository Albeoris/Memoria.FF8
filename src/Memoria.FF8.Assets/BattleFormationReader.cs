using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF8.Assets;

/// <summary>The original scene.out record, including disabled reinforcement slots.</summary>
public static class BattleFormationReader
{
    public static BattleEncounter Read(int encounterId, ReadOnlySpan<byte> originalFormation)
    {
        if ((uint)encounterId >= 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(encounterId));
        }
        if (originalFormation.Length != 128)
        {
            throw new InvalidDataException("An original battle formation contains exactly 128 bytes.");
        }
        BattleEnemyPlacement[] slots = new BattleEnemyPlacement[8];
        for (int slot = 0; slot < slots.Length; slot++)
        {
            int mask = 0x80 >> slot;
            ReadOnlySpan<byte> position = originalFormation.Slice(8 + 6 * slot, 6);
            // 48AEB0 maps bit7 to scene slot0. 47D9E0 and 48BA10 then read coordinates
            // and level at that same slot, without reversing either array.
            Vector3 coordinates = new Vector3(BinaryPrimitives.ReadInt16LittleEndian(position), -BinaryPrimitives.ReadInt16LittleEndian(position[2..]), -BinaryPrimitives.ReadInt16LittleEndian(position[4..])) / 100f;
            slots[slot] = new BattleEnemyPlacement(slot, originalFormation[56 + slot] - 16, coordinates, originalFormation[120 + slot], (originalFormation[4] & mask) != 0, (originalFormation[6] & mask) != 0)
            {
                NativePosition = new(BinaryPrimitives.ReadInt16LittleEndian(position), BinaryPrimitives.ReadInt16LittleEndian(position[2..]), BinaryPrimitives.ReadInt16LittleEndian(position[4..])),
                Enabled = (originalFormation[7] & mask) != 0,
                // 48BA10 stores this as actor flag0x80; 47D970 still loads its model.
                // 47D8A0 defers its initial AI; 487670 clears it on explicit activation.
                InitiallySuspended = (originalFormation[5] & mask) != 0
            };
        }
        return new BattleEncounter(encounterId, originalFormation[0], originalFormation[1], originalFormation[2], originalFormation[3], Array.AsReadOnly(slots.Where(slot => slot.Enabled).ToArray())) { Slots = Array.AsReadOnly(slots) };
    }
}
