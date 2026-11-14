using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAccessoryEquipmentTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original stat accessory equipment: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7SessionSave source = original.Capture();
        byte[] record = source.Progress.Characters.Single(character => character.Id == 1).NativeRecord.ToArray();
        record[30] = byte.MaxValue;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 0x12345632);
        Ff7Progress progress = source.Progress with
        {
            Party = [0, 1, 255],
            Characters = source.Progress.Characters.Select(character => character.Id == 1 ? character with { NativeRecord = record } : character).ToArray(),
            Inventory = [new(288, 1), new(289, 1), new(291, 2), new(298, 1)]
        };
        progress = Ff7NativePartyRecovery.Publish(progress, Ff7MateriaResources.ReadRecords(kernel), executable.NativeMateriaStatModifiers).Progress;
        Ff7SessionSave fixture = source with { Progress = progress, Process = source.Process with { PlayerLocked = false, MenuDisabled = false, Movie = null } };
        using Ff7Session session = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(fixture))!);
        string prior = JsonSerializer.Serialize(fixture);
        Ff7CharacterState bare = Character();
        Ff7BattlePlayerStats baseline = Stats(bare);
        Check(session.MenuEquipmentChoices(1, Ff7EquipmentSlot.Accessory).Single(choice => choice.ItemId == 291).UnavailableReason is null, "Original Talisman must be available.");
        Check(session.MenuEquipmentChoices(1, Ff7EquipmentSlot.Accessory).Single(choice => choice.ItemId == 289).UnavailableReason is null, "Original Protect Vest must be available.");
        Equip(291, 3);
        Check(Stats(Character()).Actor.MagicDefense == baseline.Actor.MagicDefense + 10, "Original Talisman did not contribute Spirit+10.");
        Check(session.Progress.Inventory.Single(item => item.ItemId == 291).Quantity == 1, "Talisman did not spend one inventory item.");
        Equip(289, 1);
        Check(Stats(Character()).Actor.Defense == baseline.Actor.Defense + 10 && Stats(Character()).Actor.MagicDefense == baseline.Actor.MagicDefense, "Original Protect Vest did not contribute only Vitality+10.");
        Check(session.Progress.Inventory.All(item => item.ItemId != 289) && session.Progress.Inventory.Single(item => item.ItemId == 291).Quantity == 2, "Accessory swap did not spend the vest and return Talisman.");
        Equip(null, byte.MaxValue);
        Check(Stats(Character()) == baseline && session.Progress.Inventory.Single(item => item.ItemId == 289).Quantity == 1, "Accessory removal did not restore baseline stats and inventory.");
        Equip(288, 0);
        Check(Stats(Character()).Actor.Attack == baseline.Actor.Attack + 10, "Original Power Wrist did not contribute Strength+10.");
        Equip(null, byte.MaxValue);
        string beforeRejected = JsonSerializer.Serialize(session.Capture());
        Check(session.MenuEquipmentChoices(1, Ff7EquipmentSlot.Accessory).Single(choice => choice.ItemId == 298).UnavailableReason is not null && !session.TryEquipMenuItem(1, Ff7EquipmentSlot.Accessory, 298), "Original Star Pendant must remain unavailable without its status-immunity consumer.");
        Check(beforeRejected == JsonSerializer.Serialize(session.Capture()), "Rejected status accessory partially changed session state.");
        Check(prior == JsonSerializer.Serialize(fixture), "Accessory transactions mutated the prior save.");
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Check(JsonSerializer.Serialize(session.Progress) == JsonSerializer.Serialize(cold.Progress), "Accessory inventory/stat state changed during JSON restoration.");
        session.AdvanceTick();
        cold.AdvanceTick();
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Accessory transaction next callback changed across JSON restoration.");

        Ff7CharacterState Character() => session.Progress.Characters.Single(character => character.Id == 1);
        Ff7BattlePlayerStats Stats(Ff7CharacterState character) => Ff7NativeBattlePlayerStats.Create(character, kernel, executable.NativeMateriaStatModifiers);

        void Equip(int? item, byte index)
        {
            byte[] expected = Character().NativeRecord.ToArray();
            expected[30] = index;
            Ff7CharacterState[] others = session.Progress.Characters.Where(character => character.Id != 1).ToArray();
            Ff7MateriaState[] materia = session.Progress.Materia.ToArray();
            Check(session.TryEquipMenuItem(1, Ff7EquipmentSlot.Accessory, item), "Supported accessory equip/swap/removal was refused.");
            Check(Character().NativeRecord.AsSpan().SequenceEqual(expected), "Accessory edit changed header, stats, weapon, armor or equipped materia bytes beyond accessory byte30.");
            Check(session.Progress.Materia.SequenceEqual(materia), "Accessory edit displaced materia into inventory.");
            Check(others.All(character => session.Progress.Characters.Single(current => current.Id == character.Id).NativeRecord.AsSpan().SequenceEqual(character.NativeRecord)), "Accessory edit changed another character's native record.");
            using Ff7Session equippedCold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
            Check(Stats(equippedCold.Progress.Characters.Single(character => character.Id == 1)) == Stats(Character()), "Equipped accessory statistics changed across JSON restoration.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
