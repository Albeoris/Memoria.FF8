using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeBattleCommandGrant(byte Command, byte MateriaId, byte Rank, byte Slot);

public static class Ff7NativeBattleCommandGrants
{
    public static IReadOnlyList<Ff7NativeBattleCommandGrant> Read(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(kernel);
        KernelSection[] sections = kernel.Where(section => section is not null && section.Type == 8).ToArray();
        if (character.NativeRecord is null || character.NativeRecord.Length != 132 || sections.Length != 1 || sections[0].Data.Length != 96 * 20)
        {
            throw new InvalidDataException("Native command grants require complete character and materia directories.");
        }
        ReadOnlySpan<byte> materia = sections[0].Data.Span;
        List<Ff7NativeBattleCommandGrant> result = [];
        HashSet<byte> commands = [];
        for (byte slot = 0; slot < 16; slot++)
        {
            uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(character.NativeRecord.AsSpan(64 + slot * 4));
            byte id = (byte)encoded;
            if (id == byte.MaxValue)
            {
                continue;
            }
            if (id >= 96)
            {
                throw new InvalidDataException("Equipped command materia references an absent native record.");
            }
            ReadOnlySpan<byte> record = materia.Slice(id * 20, 20);
            if ((record[13] & 15) != 6)
            {
                continue;
            }
            ValidateRecord(record);
            byte rank = checked((byte)Ff7NativeMateriaInventory.ReadLevel(record, encoded >> 8));
            bool found = false;
            for (int index = rank - 1; index >= 0; index--)
            {
                byte command = record[14 + index];
                if (command == byte.MaxValue)
                {
                    continue;
                }
                found = true;
                if (commands.Add(command))
                {
                    result.Add(new(command, id, rank, slot));
                }
                break;
            }
            if (!found)
            {
                throw new InvalidDataException("Command materia has no reachable grant at its native AP rank.");
            }
        }
        return result.AsReadOnly();
    }

    private static void ValidateRecord(ReadOnlySpan<byte> record)
    {
        foreach (byte command in record.Slice(14, 5))
        {
            if (command >= 32 && command != byte.MaxValue)
            {
                throw new InvalidDataException("Command materia grants an absent native command identity.");
            }
        }
    }
}
