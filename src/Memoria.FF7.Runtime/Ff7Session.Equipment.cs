using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public enum Ff7EquipmentSlot
{
    Weapon,
    Armor,
    Accessory
}

public sealed record Ff7EquipmentChoice(int? ItemId, int Quantity, string? UnavailableReason);

public sealed partial class Ff7Session
{
    public int? EquippedMenuItem(Ff7CharacterState character, Ff7EquipmentSlot slot)
    {
        var layout = EquipmentLayout(slot);
        int index = character.NativeRecord[layout.CharacterOffset];
        if (index == byte.MaxValue)
        {
            return null;
        }
        if (index >= layout.Count)
        {
            throw new InvalidDataException("Equipped FFVII gear references an absent original kernel record.");
        }
        return layout.FirstItemId + index;
    }

    public IReadOnlyList<Ff7EquipmentChoice> MenuEquipmentChoices(int characterId, Ff7EquipmentSlot slot)
    {
        if (FindNativeEquipmentCharacter(characterId) is not { } character)
        {
            return [];
        }
        var layout = EquipmentLayout(slot);
        _ = EquipmentRecords(layout.Section, layout.Stride, layout.Count);
        int? equipped = EquippedMenuItem(character, slot);
        List<Ff7EquipmentChoice> choices = [];
        if (slot == Ff7EquipmentSlot.Accessory && equipped.HasValue)
        {
            choices.Add(new(null, 0, EquipmentChangeRestriction(character, slot, equipped, null)));
        }
        foreach (Ff7InventorySlot item in Progress.Inventory)
        {
            if (item.Quantity > 0 && IsCompatibleEquipment(characterId, slot, item.ItemId))
            {
                choices.Add(new(item.ItemId, item.Quantity, EquipmentChangeRestriction(character, slot, equipped, item)));
            }
        }
        return choices.AsReadOnly();
    }

    public bool TryEquipMenuItem(int characterId, Ff7EquipmentSlot slot, int? itemId)
    {
        if (!CanUsePlayerMenu || FindNativeEquipmentCharacter(characterId) is not { } character)
        {
            return false;
        }
        Ff7InventorySlot? incoming = itemId is int selected ? Progress.Inventory.SingleOrDefault(item => item.ItemId == selected && item.Quantity > 0) : null;
        if (itemId.HasValue && (incoming is null || !IsCompatibleEquipment(characterId, slot, itemId.Value)))
        {
            return false;
        }
        int? equipped = EquippedMenuItem(character, slot);
        if (EquipmentChangeRestriction(character, slot, equipped, incoming) is not null)
        {
            return false;
        }

        Ff7InventorySlot[] inventory = Progress.Inventory;
        if (incoming is not null)
        {
            inventory = incoming.Quantity == 1
                ? inventory.Where(item => item.ItemId != incoming.ItemId).ToArray()
                : inventory.Select(item => item.ItemId == incoming.ItemId ? item with { Quantity = item.Quantity - 1 } : item).ToArray();
        }
        if (equipped is int returned)
        {
            inventory = Ff7NativeInventory.Acquire(inventory, (ushort)(returned | 512));
        }

        var layout = EquipmentLayout(slot);
        byte[] record = character.NativeRecord.ToArray();
        record[layout.CharacterOffset] = incoming is null ? byte.MaxValue : (byte)(incoming.ItemId - layout.FirstItemId);
        Ff7MateriaState[] materia = Progress.Materia;
        byte materiaFlags = 0;
        if (incoming is not null)
        {
            foreach ((int offset, uint encoded) in DisplacedMateria(character, slot, incoming.ItemId))
            {
                int id = (byte)encoded;
                if (id >= materiaTypes.Length)
                {
                    throw new InvalidDataException("Equipped FFVII materia references an absent original kernel record.");
                }
                materia = Ff7NativeMateriaInventory.Acquire(materia, id, encoded >> 8);
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), uint.MaxValue);
                if (executable.NativeMateriaClasses[materiaTypes[id] & 15] == 10)
                {
                    materiaFlags |= 1;
                }
                if (id == 44)
                {
                    materiaFlags |= 2;
                }
            }
        }
        Ff7Progress candidate = Progress with
        {
            Characters = Progress.Characters.Select(current => current.Id == characterId ? current with { NativeRecord = record } : current).ToArray(),
            Inventory = inventory,
            Materia = materia
        };
        candidate.Validate();
        PublishNativeMenuPartyStatistics(candidate, characterId, materiaFlags);
        return true;
    }

    private string? EquipmentChangeRestriction(Ff7CharacterState character, Ff7EquipmentSlot slot, int? equipped, Ff7InventorySlot? incoming)
    {
        if (incoming is null)
        {
            if (slot != Ff7EquipmentSlot.Accessory || !equipped.HasValue)
            {
                return "There is no removable gear in this slot.";
            }
            byte flags = World?.ReadPersistentByte(512 + 0x6F) ?? Machine.Variables.ReadByte(FieldBank.Persistent2Byte, 0x6F);
            if ((flags & 1) != 0)
            {
                return "Accessory removal is currently unavailable.";
            }
        }
        else
        {
            if (incoming.ItemId == equipped)
            {
                return "This gear is already equipped.";
            }
            if (slot == Ff7EquipmentSlot.Accessory)
            {
                return "FFVII accessory effects are unavailable in battle.";
            }
            if (Progress.Materia.Length + DisplacedMateria(character, slot, incoming.ItemId).Count > 200)
            {
                return "Make room in materia inventory for the displaced materia.";
            }
        }
        if (equipped is int returned)
        {
            Ff7InventorySlot? stack = Progress.Inventory.SingleOrDefault(item => item.ItemId == returned);
            if (stack?.Quantity >= 99 || stack is null && Progress.Inventory.Length == Ff7NativeInventory.SlotCount && incoming?.Quantity != 1)
            {
                return "Make room in item inventory for the equipped gear.";
            }
        }
        return null;
    }

    private Ff7CharacterState? FindNativeEquipmentCharacter(int characterId)
    {
        if (!Progress.Party.Contains(characterId) || GuestFieldVitals?.StoryCharacters.Contains(characterId) == true)
        {
            return null;
        }
        return Progress.Characters.SingleOrDefault(character => character.Id == characterId);
    }

    private bool IsCompatibleEquipment(int characterId, Ff7EquipmentSlot slot, int itemId)
    {
        var layout = EquipmentLayout(slot);
        int index = itemId - layout.FirstItemId;
        if ((uint)index >= layout.Count)
        {
            return false;
        }
        ReadOnlySpan<byte> records = EquipmentRecords(layout.Section, layout.Stride, layout.Count);
        ushort mask = BinaryPrimitives.ReadUInt16LittleEndian(records[(index * layout.Stride + layout.CompatibilityOffset)..]);
        return (mask & (1 << characterId)) != 0;
    }

    private List<(int Offset, uint Encoded)> DisplacedMateria(Ff7CharacterState character, Ff7EquipmentSlot slot, int incoming)
    {
        var layout = EquipmentLayout(slot);
        ReadOnlySpan<byte> records = EquipmentRecords(layout.Section, layout.Stride, layout.Count);
        ReadOnlySpan<byte> slots = records.Slice((incoming - layout.FirstItemId) * layout.Stride + layout.KernelMateriaOffset, 8);
        List<(int Offset, uint Encoded)> displaced = [];
        for (int index = 0; index < slots.Length; index++)
        {
            int offset = layout.CharacterMateriaOffset + index * 4;
            uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(character.NativeRecord.AsSpan(offset));
            if (slots[index] == 0 && encoded != uint.MaxValue)
            {
                displaced.Add((offset, encoded));
            }
        }
        return displaced;
    }

    private ReadOnlySpan<byte> EquipmentRecords(int section, int stride, int count)
    {
        battleKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        if (battleKernel.Count <= section || battleKernel[section].Data.Length != stride * count)
        {
            throw new InvalidDataException("FFVII Equip requires the original equipment kernel records.");
        }
        return battleKernel[section].Data.Span;
    }

    private static (int CharacterOffset, int FirstItemId, int Section, int Stride, int Count, int CompatibilityOffset, int CharacterMateriaOffset, int KernelMateriaOffset) EquipmentLayout(Ff7EquipmentSlot slot) => slot switch
    {
        Ff7EquipmentSlot.Weapon => (28, 128, 5, 44, 128, 14, 64, 28),
        Ff7EquipmentSlot.Armor => (29, 256, 6, 36, 32, 18, 96, 9),
        Ff7EquipmentSlot.Accessory => (30, 288, 7, 16, 32, 12, 0, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}
