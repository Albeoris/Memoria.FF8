using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7EquippedMateriaSlot(int Slot, bool Linked, int MateriaId, uint AbilityPoints);

public sealed partial class Ff7Session
{
    public IReadOnlyList<Ff7EquippedMateriaSlot> EquippedMateria(int characterId)
    {
        Ff7CharacterState character = Progress.Characters.Single(character => character.Id == characterId && Progress.Party.Contains(characterId));
        ReadOnlySpan<byte> record = character.NativeRecord;
        battleKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        ReadOnlySpan<byte> weapon = battleKernel[5].Data.Span.Slice(record[28] * 44 + 0x1C, 8);
        ReadOnlySpan<byte> armor = battleKernel[6].Data.Span.Slice(record[29] * 36 + 9, 8);
        List<Ff7EquippedMateriaSlot> slots = [];
        for (int slot = 0; slot < 16; slot++)
        {
            int kind = slot < 8 ? weapon[slot] & 3 : armor[slot - 8] & 3;
            if (kind != 0)
            {
                uint materia = BinaryPrimitives.ReadUInt32LittleEndian(record[(64 + slot * 4)..]);
                slots.Add(new(slot, kind is 2 or 3, (byte)materia, materia >> 8));
            }
        }
        return slots.AsReadOnly();
    }

    public bool EquipMateria(int characterId, int equippedSlot, int? inventorySlot)
    {
        if (Battle is not null || Host.Process.GlobalCommand != Ff7GlobalCommand.None || GuestFieldVitals?.StoryCharacters.Contains(characterId) == true)
        {
            return false;
        }
        Ff7EquippedMateriaSlot? equipped = EquippedMateria(characterId).SingleOrDefault(candidate => candidate.Slot == equippedSlot);
        if (equipped is null)
        {
            return false;
        }
        List<Ff7MateriaState> inventory = [.. Ff7NativeMateriaInventory.Normalize(Progress.Materia)];
        Ff7MateriaState? replacement = inventorySlot is int slot ? inventory.SingleOrDefault(materia => materia.NativeSlot == slot) : null;
        if (inventorySlot.HasValue && replacement is null || replacement is null && (equipped.MateriaId == 255 || inventory.Count == 200))
        {
            return false;
        }
        if (replacement is not null)
        {
            inventory.Remove(replacement);
        }
        if (equipped.MateriaId != 255)
        {
            int freeSlot = inventorySlot ?? Enumerable.Range(0, 200).First(slot => inventory.All(materia => materia.NativeSlot != slot));
            inventory.Add(new(equipped.MateriaId, equipped.AbilityPoints, freeSlot));
        }
        Ff7CharacterState[] characters = (Ff7CharacterState[])Progress.Characters.Clone();
        int index = Array.FindIndex(characters, character => character.Id == characterId);
        byte[] native = (byte[])characters[index].NativeRecord.Clone();
        uint encoded = replacement is null ? uint.MaxValue : replacement.AbilityPoints << 8 | (uint)replacement.MateriaId;
        BinaryPrimitives.WriteUInt32LittleEndian(native.AsSpan(64 + equippedSlot * 4), encoded);
        characters[index] = characters[index] with { NativeRecord = native };
        Ff7Progress candidate = Progress with { Characters = characters, Materia = Ff7NativeMateriaInventory.Normalize(inventory) };
        candidate.Validate();
        Ff7PartyRecoveryResult adjusted = Ff7NativePartyRecovery.Publish(candidate, Ff7MateriaResources.LoadRecords(installation), executable.NativeMateriaStatModifiers);
        Progress = PreserveGuestFieldCharacters(Progress, adjusted.Progress);
        return true;
    }

    private FieldHostReply AcquireMateria(FieldMateriaCommand command)
    {
        if (Progress.Materia.Length == 200)
        {
            return new(true, unchecked((int)command.Encoded));
        }
        int id = (byte)command.Encoded;
        if (id >= materiaTypes.Length)
        {
            throw new NotSupportedException("FFVII acquired materia references an absent original kernel record.");
        }
        Progress = Progress with { Materia = Ff7NativeMateriaInventory.Acquire(Progress.Materia, id, command.Encoded >> 8) };
        int flags = executable.NativeMateriaClasses[materiaTypes[id] & 15] == 10 ? 1 : 0;
        if (id == 44)
        {
            flags |= 2;
        }
        if (flags != 0)
        {
            byte current = Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 75);
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 75, (byte)(current | flags));
        }
        return new(true, -1);
    }
}
