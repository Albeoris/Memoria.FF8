using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed record Ff7EquipmentRemovalResult(Ff7Progress Progress, byte MateriaFlags);

public static class Ff7NativeEquipmentRemoval
{
    public static Ff7EquipmentRemovalResult Apply(Ff7Progress progress, int characterId, IReadOnlyList<byte> materiaTypes, IReadOnlyList<byte> materiaClasses)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(materiaTypes);
        ArgumentNullException.ThrowIfNull(materiaClasses);
        Ff7CharacterState character = progress.Characters.SingleOrDefault(character => character.Id == characterId) ?? throw new NotSupportedException("FFVII equipment removal requires the selected native character record.");
        byte[] record = character.NativeRecord.ToArray();
        List<Ff7MateriaState> inventory = progress.Materia.ToList();
        byte flags = 0;
        for (int offset = 64; offset < 128; offset += 4)
        {
            uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));
            if (encoded == uint.MaxValue)
            {
                continue;
            }
            if (inventory.Count < 200)
            {
                int id = (byte)encoded;
                if (id >= materiaTypes.Count || (materiaTypes[id] & 15) >= materiaClasses.Count)
                {
                    throw new InvalidDataException("FFVII equipped materia requires its original class record.");
                }
                inventory.Add(new(id, encoded >> 8));
                if (materiaClasses[materiaTypes[id] & 15] == 10)
                {
                    flags |= 1;
                }
                if (id == 44)
                {
                    flags |= 2;
                }
            }
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), uint.MaxValue);
        }
        Ff7InventorySlot[] items = progress.Inventory;
        if (record[30] != byte.MaxValue)
        {
            items = Ff7NativeInventory.Acquire(items, (ushort)((record[30] + 288) | 512));
            record[30] = byte.MaxValue;
        }
        Ff7Progress changed = progress with
        {
            Characters = progress.Characters.Select(current => current.Id == characterId ? current with { NativeRecord = record } : current).ToArray(),
            Inventory = items,
            Materia = inventory.ToArray()
        };
        changed.Validate();
        return new(changed, flags);
    }
}
