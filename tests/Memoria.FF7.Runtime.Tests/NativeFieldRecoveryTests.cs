using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeFieldRecoveryTests
{
    public static void Run()
    {
        KernelSection[] kernel = Kernel();
        CheckItems(kernel);
        CheckMagic(kernel);
    }

    private static void CheckItems(KernelSection[] kernel)
    {
        Ff7CharacterState injured = Character(0, 500, 150);
        Ff7CharacterState down = Character(1, 0, 30);
        Ff7CharacterState reserve = Character(2, 100, 60);
        Ff7Progress original = Progress(injured, down, reserve);
        foreach ((int item, int hp, int mp) in new[] { (0, 600, 150), (1, 1000, 150), (2, 1001, 150), (3, 500, 200), (4, 500, 200), (5, 1001, 200) })
        {
            Check(Ff7NativeFieldRecovery.TryUseItem(original, item, 0, kernel, null, out Ff7Progress result), $"Item {item} must apply.");
            CheckVitals(result.Characters[0], hp, mp);
            Check(result.Inventory.Single(slot => slot.ItemId == item).Quantity == 1, "Each successful use spends one original item.");
            Check(ReferenceEquals(down, result.Characters[1]) && ReferenceEquals(reserve, result.Characters[2]), "Other character records must retain their owners.");
            CheckVitals(injured, 500, 150);
        }

        Check(Ff7NativeFieldRecovery.TryUseItem(original, 7, 1, kernel, null, out Ff7Progress revived), "Phoenix Down must revive a downed resident.");
        CheckVitals(revived.Characters[1], 250, 30);
        Check(revived.Inventory.All(item => item.ItemId != 7), "An exhausted stack must disappear.");
        Check(revived.FieldRandomIndex == original.FieldRandomIndex && revived.Gil == original.Gil, "Recovery must preserve RNG and unrelated progression.");

        foreach ((int item, int target, int[]? guests) in new (int, int, int[]?)[] { (0, 1, null), (3, 1, null), (7, 0, null), (6, 0, null), (8, 0, null), (0, 2, null), (0, 0, [0]) })
        {
            Check(!Ff7NativeFieldRecovery.TryUseItem(original, item, target, kernel, guests, out Ff7Progress rejected) && ReferenceEquals(original, rejected), $"Invalid item {item} on {target} must preserve the exact state.");
        }

        Ff7Progress full = Progress(Character(0, 1001, 200), down, reserve);
        Check(!Ff7NativeFieldRecovery.TryUseItem(full, 0, 0, kernel, null, out Ff7Progress unchanged) && ReferenceEquals(full, unchanged), "A full target must cost no stock.");
        Check(!Ff7NativeFieldRecovery.TryUseItem(revived, 7, 1, kernel, null, out unchanged) && ReferenceEquals(revived, unchanged), "Repeating a spent Phoenix Down must preserve the result.");
    }

    private static void CheckMagic(KernelSection[] kernel)
    {
        Ff7CharacterState caster = Character(0, 40, 30);
        Ff7CharacterState target = Character(1, 10, 30);
        Ff7CharacterState reserve = Character(2, 10, 30);
        Ff7Progress original = Progress(caster, target, reserve);
        caster.NativeRecord[4] = 16;
        caster.NativeRecord[10] = 2;
        caster.NativeRecord[30] = 0;
        caster.NativeRecord[64] = 53;
        caster.NativeRecord[68] = 26;
        caster.NativeRecord[72] = 3;
        short[] modifiers = new short[17 * 8];
        modifiers[11 * 8 + 2] = 1;

        Ff7BattleSpell spell = Ff7NativeFieldRecovery.ReadMagic(caster, kernel).Single();
        Check(spell.AttackId == 0 && spell.Attack.CastingCost == 6, "A linked level-one MP Turbo adds one MP to Cure.");
        Check(Ff7NativeFieldRecovery.TryCastMagic(original, 0, 0, 1, kernel, modifiers, null, out Ff7Progress cast), "Cure must restore a living target.");
        CheckVitals(cast.Characters[0], 40, 24);
        CheckVitals(cast.Characters[1], 336, 30);
        Check(ReferenceEquals(original.Inventory, cast.Inventory) && ReferenceEquals(reserve, cast.Characters[2]), "Casting must preserve inventory and reserve records.");

        Check(Ff7NativeFieldRecovery.TryCastMagic(original, 0, 0, 0, kernel, modifiers, null, out Ff7Progress self), "Cure must atomically debit and heal its own caster.");
        CheckVitals(self.Characters[0], 366, 24);
        CheckVitals(caster, 40, 30);
        CheckVitals(target, 10, 30);

        foreach ((int source, int id, int recipient, int[]? guests) in new (int, int, int, int[]?)[] { (0, 1, 1, null), (0, 27, 1, null), (1, 0, 0, null), (2, 0, 1, null), (0, 0, 2, null), (0, 0, 1, [0]), (0, 0, 1, [1]) })
        {
            Check(!Ff7NativeFieldRecovery.TryCastMagic(original, source, id, recipient, kernel, modifiers, guests, out Ff7Progress rejected) && ReferenceEquals(original, rejected), "An unequipped spell or ineligible party member must cost no MP.");
        }

        Ff7CharacterState depleted = Character(0, 40, 4);
        depleted.NativeRecord[64] = 53;
        Ff7Progress tooLittleMp = Progress(depleted, target, reserve);
        Check(!Ff7NativeFieldRecovery.TryCastMagic(tooLittleMp, 0, 0, 1, kernel, modifiers, null, out Ff7Progress unchanged) && ReferenceEquals(tooLittleMp, unchanged), "Cure needs at least its exact MP cost.");

        Ff7CharacterState down = Character(0, 0, 34);
        down.NativeRecord[64] = 55;
        Ff7Progress downed = Progress(down, target, reserve);
        Check(Ff7NativeFieldRecovery.TryCastMagic(downed, 0, 7, 0, kernel, modifiers, null, out Ff7Progress revived), "Original field Life can spend the downed caster's remaining MP.");
        CheckVitals(revived.Characters[0], 250, 0);
    }

    private static KernelSection[] Kernel()
    {
        byte[] attacks = new byte[128 * Ff7BattleAttack.RecordSize];
        attacks[4] = 5;
        attacks[7 * Ff7BattleAttack.RecordSize + 4] = 34;
        byte[] weapons = new byte[128 * 44];
        weapons[20] = 2;
        weapons[24] = 3;
        weapons[28] = 2;
        weapons[29] = 3;
        byte[] accessories = new byte[32 * 16];
        accessories[0] = 2;
        accessories[2] = 2;
        byte[] materia = new byte[96 * 20];
        materia.AsSpan().Fill(byte.MaxValue);
        materia[53 * 20 + 8] = 11;
        materia[53 * 20 + 13] = 0x19;
        materia[53 * 20 + 14] = 0;
        materia[55 * 20 + 13] = 0x19;
        materia[55 * 20 + 14] = 7;
        materia[26 * 20 + 13] = 0x25;
        materia[26 * 20 + 14] = 0x58;
        materia[3 * 20 + 13] = 0x20;
        materia[3 * 20 + 14] = 2;
        materia[3 * 20 + 15] = 10;
        return [new(1, attacks), new(4, new byte[128 * Ff7BattleAttack.RecordSize]), new(5, weapons), new(6, new byte[32 * 36]), new(7, accessories), new(8, materia)];
    }

    private static Ff7Progress Progress(params Ff7CharacterState[] characters)
    {
        return new()
        {
            InitialKernelData = [1],
            Characters = characters,
            Party = [0, 1, 255],
            Inventory = Enumerable.Range(0, 9).Select(id => new Ff7InventorySlot(id, id == 7 ? 1 : 2)).ToArray(),
            Materia = [],
            Gil = 97,
            FieldRandomIndex = 8
        };
    }

    private static Ff7CharacterState Character(int id, ushort hp, ushort mp)
    {
        byte[] native = new byte[132];
        native[1] = 10;
        native[30] = byte.MaxValue;
        native[31] = 0x30;
        native.AsSpan(64, 64).Fill(byte.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(44), hp);
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(48), mp);
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(56), 1001);
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(58), 200);
        return new(id, $"Member {id}", 10, 0, hp, mp, native);
    }

    private static void CheckVitals(Ff7CharacterState character, int hp, int mp)
    {
        Check(character.HitPoints == hp && BinaryPrimitives.ReadUInt16LittleEndian(character.NativeRecord.AsSpan(44)) == hp, "Saved HP and native HP must agree.");
        Check(character.MagicPoints == mp && BinaryPrimitives.ReadUInt16LittleEndian(character.NativeRecord.AsSpan(48)) == mp, "Saved MP and native MP must agree.");
        Check(character.NativeRecord[31] == 0x30, "Field recovery must preserve saved status flags.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
