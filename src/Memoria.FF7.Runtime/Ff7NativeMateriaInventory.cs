using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public static class Ff7NativeMateriaInventory
{
    internal static int ReadLevel(ReadOnlySpan<byte> materia, uint abilityPoints)
    {
        for (int index = 3; index >= 0; index--)
        {
            ushort threshold = BinaryPrimitives.ReadUInt16LittleEndian(materia[(index * 2)..]);
            if (threshold != ushort.MaxValue && abilityPoints >= threshold * 100u)
            {
                return index + 2;
            }
        }
        return 1;
    }

    public static Ff7MateriaState[] Normalize(IReadOnlyList<Ff7MateriaState> inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.Count > 200)
        {
            throw new InvalidDataException("The native materia inventory exceeds200 slots.");
        }
        HashSet<int> occupied = [];
        foreach (Ff7MateriaState materia in inventory)
        {
            if (materia.NativeSlot is int slot && (slot is < 0 or >= 200 || !occupied.Add(slot)))
            {
                throw new InvalidDataException("The native materia slot identity is invalid or duplicated.");
            }
        }
        List<Ff7MateriaState> normalized = new(inventory.Count);
        foreach (Ff7MateriaState materia in inventory)
        {
            int slot = materia.NativeSlot ?? Enumerable.Range(0, 200).First(candidate => !occupied.Contains(candidate));
            occupied.Add(slot);
            normalized.Add(materia with { NativeSlot = slot });
        }
        return normalized.OrderBy(materia => materia.NativeSlot).ToArray();
    }

    public static Ff7MateriaState[] Acquire(IReadOnlyList<Ff7MateriaState> inventory, int materiaId, uint abilityPoints)
    {
        if (materiaId is < 0 or > 254 || abilityPoints > 0xFFFFFF)
        {
            throw new InvalidDataException("The native encoded materia is invalid.");
        }
        Ff7MateriaState[] normalized = Normalize(inventory);
        if (normalized.Length == 200)
        {
            return normalized;
        }
        int slot = Enumerable.Range(0, 200).First(candidate => normalized.All(materia => materia.NativeSlot != candidate));
        return normalized.Append(new(materiaId, abilityPoints, slot)).OrderBy(materia => materia.NativeSlot).ToArray();
    }

    public static Ff7MateriaState[] Erase(IReadOnlyList<Ff7MateriaState> inventory, int nativeSlot, int materiaId)
    {
        Ff7MateriaState[] normalized = Normalize(inventory);
        Ff7MateriaState selected = normalized.Single(materia => materia.NativeSlot == nativeSlot);
        if (selected.MateriaId != materiaId)
        {
            throw new InvalidDataException("The selected native materia slot changed its identity.");
        }
        return normalized.Where(materia => materia.NativeSlot != nativeSlot).ToArray();
    }
}
