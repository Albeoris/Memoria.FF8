using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMenuEquipmentTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original-resource Equip menu transactions: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7SessionSave source = original.Capture();
        byte[] cloud = source.Progress.Characters.Single(character => character.Id == 0).NativeRecord.ToArray();
        cloud[28] = 2;
        cloud[29] = 2;
        cloud[30] = 0;
        cloud.AsSpan(64, 64).Fill(byte.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(cloud.AsSpan(64), 0x12345632);
        BinaryPrimitives.WriteUInt32LittleEndian(cloud.AsSpan(72), 0x6543212C);
        BinaryPrimitives.WriteUInt32LittleEndian(cloud.AsSpan(76), 0xABCDEF11);
        BinaryPrimitives.WriteUInt32LittleEndian(cloud.AsSpan(100), 0xFFFFFE34);
        byte[] variables = source.Vm.PersistentVariables.ToArray();
        variables[75] = 0xA0;
        variables[122] = 0xC0;
        variables[512 + 0x6F] = 0;
        Ff7SessionSave fixture = source with
        {
            Process = source.Process with { PlayerLocked = false, MenuDisabled = false, Movie = null },
            Vm = source.Vm with { PersistentVariables = variables },
            Progress = source.Progress with
            {
                Party = [0, 1, 255],
                Characters = source.Progress.Characters.Select(character => character.Id == 0 ? character with { NativeRecord = cloud } : character).ToArray(),
                Inventory = [new(128, 2), new(130, 3), new(161, 1), new(257, 1), new(289, 1), new(131, 0)],
                Materia = [new(49, 6, 1)]
            }
        };
        FieldTransactions(installation, fixture);
        RejectedTransactions(installation, fixture);
        WorldTransactions(installation, fixture);
        Console.WriteLine("PASS original-resource Equip compatibility, materia and AP transfer, stock, field/world admission and cold continuation");
    }

    private static void FieldTransactions(Ff7Installation installation, Ff7SessionSave fixture)
    {
        using Ff7Session session = Ff7Session.Restore(installation, fixture);
        session.GuestFieldVitals = new Guest();
        Ff7CharacterState dormant = session.Progress.Characters.Single(character => character.Id == 1);
        Ff7CharacterState reserve = session.Progress.Characters.Single(character => character.Id == 2);
        Check(session.CanUsePlayerMenu, "The fixture must admit ordinary field menu edits.");
        Check(session.MenuEquipmentChoices(0, Ff7EquipmentSlot.Weapon).Select(choice => choice.ItemId).SequenceEqual(new int?[] { 128, 130 }), "Weapon choices must use the original character compatibility mask and positive stock.");
        Check(session.MenuEquipmentChoices(1, Ff7EquipmentSlot.Weapon).Count == 0 && session.MenuEquipmentChoices(2, Ff7EquipmentSlot.Weapon).Count == 0, "Guests and reserve members must have no Equip choices.");
        Check(session.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 128), "Cloud's compatible replacement weapon was refused.");
        byte[] record = session.Progress.Characters.Single(character => character.Id == 0).NativeRecord;
        Check(record[28] == 0 && record[29] == 2 && record[30] == 0, "Equipping the weapon changed other equipment slots.");
        Check(ReadMateria(record, 64) == 0x12345632 && ReadMateria(record, 72) == uint.MaxValue && ReadMateria(record, 76) == uint.MaxValue && ReadMateria(record, 100) == 0xFFFFFE34, "Only lost slots in the selected equipment row should lose their materia.");
        Check(session.Progress.Materia.SequenceEqual(new Ff7MateriaState[] { new(44, 0x654321, 0), new(49, 6, 1), new(17, 0xABCDEF, 2) }), "Displaced materia did not return to the first free physical slots in order with all AP bits.");
        Check(session.Progress.Inventory[0] == new Ff7InventorySlot(128, 1) && session.Progress.Inventory[1] == new Ff7InventorySlot(130, 4), "Weapon inventory did not spend one replacement and return one equipped weapon.");
        Check(session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 75) == 0xA2 && session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 122) == 0x40, "The Equip update did not publish the native materia and Underwater flags.");
        Check(session.TryEquipMenuItem(0, Ff7EquipmentSlot.Armor, 257), "Cloud's compatible replacement armor was refused.");
        record = session.Progress.Characters.Single(character => character.Id == 0).NativeRecord;
        Check(record[29] == 1 && ReadMateria(record, 100) == uint.MaxValue && session.Progress.Materia[^1] == new Ff7MateriaState(52, 0xFFFFFE, 3), "Armor did not return its displaced materia with all AP bits.");
        Check(session.Progress.Inventory.All(item => item.ItemId != 257) && session.Progress.Inventory.Contains(new Ff7InventorySlot(258, 1)), "Armor inventory did not erase spent stock and return the previous armor.");
        Check(session.MenuEquipmentChoices(0, Ff7EquipmentSlot.Accessory).Single(choice => choice.ItemId == 289).UnavailableReason is not null, "Accessory effects without battle support must remain unavailable.");
        Check(!session.TryEquipMenuItem(0, Ff7EquipmentSlot.Accessory, 289) && session.TryEquipMenuItem(0, Ff7EquipmentSlot.Accessory, null), "Equip must permit clearing an accessory without equipping unsupported accessory effects.");
        Check(session.Progress.Characters.Single(character => character.Id == 0).NativeRecord[30] == byte.MaxValue && session.Progress.Inventory.Contains(new Ff7InventorySlot(288, 1)), "Removing the accessory did not restore it to item inventory.");
        Check(ReferenceEquals(dormant, session.Progress.Characters.Single(character => character.Id == 1)) && ReferenceEquals(reserve, session.Progress.Characters.Single(character => character.Id == 2)), "Equip updated a dormant guest or reserve character record.");
        Check(fixture.Progress.Characters[0].NativeRecord[28] == 2 && fixture.Progress.Inventory[0].Quantity == 2 && fixture.Progress.Materia.Length == 1, "Equip mutated the prior saved state.");
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Check(JsonSerializer.Serialize(cold.Progress) == JsonSerializer.Serialize(session.Progress), "Equipment, inventory and effective stats did not survive cold restoration.");
    }

    private static void RejectedTransactions(Ff7Installation installation, Ff7SessionSave fixture)
    {
        Ff7MateriaState[] full = Enumerable.Range(0, 199).Select(slot => new Ff7MateriaState(49, (uint)slot, slot)).ToArray();
        using Ff7Session capacity = Ff7Session.Restore(installation, fixture with { Progress = fixture.Progress with { Materia = full } });
        Ff7Progress before = capacity.Progress;
        Check(capacity.MenuEquipmentChoices(0, Ff7EquipmentSlot.Weapon).Single(choice => choice.ItemId == 128).UnavailableReason is not null && !capacity.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 128), "Weapon changes must refuse when two displaced materia cannot fit into the one free slot.");
        Check(!capacity.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 161) && !capacity.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 130) && !capacity.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 131), "Equip accepted incompatible, already-equipped, or empty stock.");
        Check(ReferenceEquals(before, capacity.Progress), "A refused Equip transaction partially wrote progression.");
        using Ff7Session saturated = Ff7Session.Restore(installation, fixture with { Progress = fixture.Progress with { Inventory = [new(128, 2), new(130, 99)] } });
        Check(!saturated.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 128), "Equip must preserve the returned item at the native stack limit.");
        using Ff7Session locked = Ff7Session.Restore(installation, fixture with { Process = fixture.Process with { MenuDisabled = true } });
        Check(!locked.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 128), "Equip must obey the native field menu gate.");
        capacity.Machine.Variables.WriteByte(FieldBank.Persistent2Byte, 0x6F, 1);
        Check(!capacity.TryEquipMenuItem(0, Ff7EquipmentSlot.Accessory, null), "Accessory removal must honor its original menu restriction bit.");
    }

    private static void WorldTransactions(Ff7Installation installation, Ff7SessionSave fixture)
    {
        byte[] persistent = fixture.Vm.PersistentVariables.ToArray();
        byte[] party = persistent.AsSpan(256 + 9, 3).ToArray();
        using Ff7WorldRuntime original = new(installation, persistent, 1, party);
        original.Advance();
        Ff7WorldProcess idle = original.Process with { MainState = 1, FadeRate = 0, FadeAlpha = 0, FadeDisplayedAlpha = 0, MovementEnabled = true, ControlLock = 0 };
        using Ff7Session session = Ff7Session.Restore(installation, fixture with { World = idle, Process = fixture.Process with { MenuDisabled = true } });
        Check(session.TryEquipMenuItem(0, Ff7EquipmentSlot.Weapon, 128), "Equip must use the active world menu gate even if the suspended field menu is disabled.");
        Check(session.World!.ReadPersistentByte(75) == 0xA2 && session.World.ReadPersistentByte(122) == 0x40, "Equip failed to publish its flags to the world owner.");
        Check(session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 75) == 0xA0 && session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 122) == 0xC0, "A world Equip update changed the suspended field flags.");
        Check(session.Capture().Vm.PersistentVariables[122] == 0x40, "Equip flags were absent from the captured world overlay.");
        session.World.Restore(session.World.Process with { ControlLock = 1 });
        Check(!session.TryEquipMenuItem(0, Ff7EquipmentSlot.Armor, 257), "Equip ignored the world control lock.");
    }

    private static uint ReadMateria(byte[] record, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Guest : IFf7GuestFieldVitals
    {
        public Ff7GuestFieldVitalsView Read() => new(1, "Guest", 1, 1, 1);

        public void Recover(bool clearStatuses)
        {
            throw new InvalidOperationException("Equip must leave guest vitals alone.");
        }

        public void AddHitPoints(short amount)
        {
            throw new InvalidOperationException("Equip must leave guest vitals alone.");
        }
    }
}
