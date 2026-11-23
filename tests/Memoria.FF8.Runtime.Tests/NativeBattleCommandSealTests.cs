namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommandSealTests
{
    [Fact]
    public void Native_slots_apply_each_seal_only_to_its_equipped_command_family()
    {
        Assert.Equal((byte)0, NativeBattleLimitRules.CommandSlotSealFlags(0, 1, 0xFF));
        Assert.Equal((byte)0, NativeBattleLimitRules.CommandSlotSealFlags(0, 12, 0xFF));
        Assert.Equal((byte)0, NativeBattleLimitRules.CommandSlotSealFlags(2, 0, 0xFF));
        (byte Command, byte Seal)[] cases = [(2, 0x02), (3, 0x04), (4, 0x01), (6, 0x08), (13, 0x01), (23, 0x10), (38, 0x10)];
        foreach ((byte command, byte seal) in cases)
        {
            Assert.Equal((byte)2, NativeBattleLimitRules.CommandSlotSealFlags(3, command, seal));
            Assert.Equal((byte)0, NativeBattleLimitRules.CommandSlotSealFlags(3, command, unchecked((byte)~seal)));
        }
    }

    [Fact]
    public void All_seals_admit_attack_and_an_item_release_restores_only_item_family()
    {
        BattleSession sealedBattle = CreateBattle(0xFF);
        BattleCombatant actor = sealedBattle.Combatants.Single(combatant => combatant.IsPlayer);
        Assert.Equal(new byte[] { 1, 4, 2, 23 }, actor.NativeCommandSlots.ToArray());
        Assert.Contains(BattleAbility.Item, actor.Abilities);
        Assert.Contains(actor.NativeCommands, binding => binding.Command == 4);
        AssertSealed(4, () => sealedBattle.QueueAction("party", "party", BattleAbility.Item, 1));
        AssertSealed(2, () => sealedBattle.QueueAction("party", "enemy", BattleAbility.Magic, 1));
        AssertSealed(6, () => sealedBattle.QueueAction("party", "enemy", BattleAbility.Draw, 1));
        AssertSealed(3, () => sealedBattle.QueueAction("party", "enemy", BattleAbility.GuardianForce, 0));
        AssertSealed(23, () => sealedBattle.QueueNativePlayerCommand("party", "party", 23, 0));
        Assert.Empty(sealedBattle.QueuedActions);
        Assert.Equal(new BattleStock(1, 1), Assert.Single(sealedBattle.Items));
        Assert.True(actor.IsReady);
        sealedBattle.QueueAttack("party", "enemy");
        Assert.Equal(BattleAbility.Attack, Assert.Single(sealedBattle.QueuedActions).Ability);

        BattleSession itemReleased = CreateBattle(0xFE);
        Assert.False(itemReleased.IsPlayerCommandSealed("party", 1));
        Assert.False(itemReleased.IsPlayerCommandSealed("party", 4));
        Assert.False(itemReleased.IsPlayerCommandSealed("party", 13));
        Assert.True(itemReleased.IsPlayerCommandSealed("party", 2));
        Assert.True(itemReleased.IsPlayerCommandSealed("party", 23));
        Assert.False(itemReleased.IsPlayerCommandSealed("enemy", 2));
        itemReleased.QueueAction("party", "party", BattleAbility.Item, 1);
        Assert.Equal(BattleAbility.Item, Assert.Single(itemReleased.QueuedActions).Ability);
        Assert.Empty(itemReleased.Items);
    }

    private static void AssertSealed(byte command, Action queue)
    {
        Assert.Equal($"Player command {command} is sealed.", Assert.Throws<InvalidOperationException>(queue).Message);
    }

    private static BattleSession CreateBattle(byte restrictions)
    {
        BattleNativeAction potion = new(4, 1, 5, 1) { Name = "Potion", MagicId = 0, AttackType = 3, Power = 20, HitCount = 1, TargetFlags = 0x10, AttackFlags = 2, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [.. new byte[24]], SupplementalRecord = [] };
        return new(new BattleDefinition
        {
            NativeFieldBattleRules = restrictions,
            Items = [new(1, 1)],
            NativeActions = [potion],
            ActionDefinitions = [new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Native, potion.Power, true) { NativeAction = potion }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeCommandSlots = [1, 4, 2, 23], NativeCommands = [new(1, "Attack", 0, 0), new(4, "Item", 0, 0), new(2, "Magic", 0, 0), new(23, "Defend", 0, 0)], Abilities = [BattleAbility.Attack, BattleAbility.Item, BattleAbility.Magic, BattleAbility.NativeCommand] },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);
    }
}
