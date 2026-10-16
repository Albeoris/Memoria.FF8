using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpecialActionTests
{
    [Fact]
    public void CardAcquiresAtInvocationAndRetainsItsPreparedResultAcrossWindupRestore()
    {
        BattleSession battle = Create(17);
        battle.QueueNativePlayerCommand("party-0", "enemy", 29, 10);
        Assert.Equal(-1, battle.Capture().NativeCards!.AvailableCount(4));
        battle.Tick();
        Assert.Equal(1, battle.Capture().NativeCards!.AvailableCount(4));
        Assert.False(battle.ActiveAction!.NativeSpecial!.Failed);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(1, battle.Capture().NativeCards!.AvailableCount(4));
        Assert.Contains(battle.Events, item => item.Kind == "NativeText" && item.Message.Contains("Card4", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void AFullCardStockFailsConversionWithoutRemovingTheEnemy()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        for (int count = 0; count < 100; count++)
        {
            cards = cards.Acquire(4, out _);
        }
        BattleSession battle = Create(17, cards);
        battle.QueueNativePlayerCommand("party-0", "enemy", 29, 10);
        Complete(battle);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(100, battle.Capture().NativeCards!.AvailableCount(4));
        Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy").IsAlive);
    }

    [Fact]
    public void DevourUsesOwnedGrowthBytesAndDoesNotRollAtItsPreparedValidation()
    {
        BattleSession battle = Create(19);
        battle.QueueNativePlayerCommand("party-0", "enemy", 7, 9);
        string before = JsonSerializer.Serialize(battle.Capture().Random);
        battle.Tick();
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture().Random));
        Complete(battle);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        BattleNativeCharacterGrowth growth = battle.Combatants[0].NativeGrowth!;
        Assert.Equal(9999, growth.HitPointBonus);
        Assert.Equal(new byte[] { 255, 2, 3, 4, 5, 6 }, growth.StatBonuses.ToArray());
        Assert.Contains(battle.Events, item => item.Kind == "NativeText" && item.Message == "Devour effect");
    }

    [Fact]
    public void AngeloSearchAdvancesTwoSeparateRandomOwnersAndAddsTheFoundItemImmediately()
    {
        BattleSession battle = Create(23);
        NativeCrtRandomState expectedCrt = NativeCrtRandomState.FromColdProcess().Draw(out ushort category);
        Assert.Equal((ushort)41, category);
        NativeBattleRandomState expectedBattle = battle.Capture().Random!.Draw(out byte roll);
        int item = (roll & 7) + 1;
        battle.QueueNativePlayerCommand("party-0", "party-0", 240, 0);
        battle.Tick();
        Assert.Equal((byte)5, battle.ActiveAction!.IndependentGuardian!.Level);
        Complete(battle);
        Assert.Equal(expectedCrt, battle.Capture().NativeCrtRandom);
        Assert.Equal(expectedBattle.Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        Assert.Equal(item, Assert.Single(battle.Items).Id);
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 40 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
    }

    private static BattleSession Create(byte type, NativeCardInventory? cards = null)
    {
        byte command = type == 17 ? (byte)29 : type == 19 ? (byte)7 : (byte)240;
        ushort id = type == 17 ? (ushort)10 : type == 19 ? (ushort)9 : (ushort)0;
        byte flags = type == 23 ? (byte)0x10 : (byte)0x50;
        BattleNativeAction action = new(command, id, 10, id) { MagicId = 1, AttackType = type, Power = 0, HitCount = 1, TargetFlags = flags, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [1], SupplementalRecord = [] };
        string?[] messages = new string?[119];
        messages[9] = "Converted";
        messages[11] = " ";
        messages[16] = "card";
        messages[50] = "Card cannot be used";
        messages[64] = " found";
        messages[71] = "";
        messages[112] = "Cannot devour";
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        // Explicit component source records exercise the native algorithms without declaring an installed encounter.
        //48FE20 LABEL83 prepares240's level from acquired GF rows before dispatch, even for Search; this fixture owns one acquired level5 GF.
        return new(new() { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, GuardianForces = type == 23 ? [new(0, 100, 100, true)] : [], ActionDefinitions = type == 23 ? [new(BattleAbility.GuardianForce, 0, "Explicit level5 GF", BattleActionEffect.Native, 0, false, Level: 5)] : [], NativeActions = [action], NativeCards = cards ?? NativeCardInventory.CreateNewGame(), NativeCrtRandom = NativeCrtRandomState.FromColdProcess(), NativeMessageSpeed = 1, NativeGlobalMessages = messages, NativeCardNames = Enumerable.Range(0, 110).Select(index => (string?)$"Card{index}").ToArray(), NativeItemNames = Enumerable.Range(0, 256).Select(index => (string?)$"Item{index}").ToArray(), NativeDevourEffects = [new(30, 0, 0, 0, 1, 10, "Devour effect")], Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeGrowth = new(9995, ImmutableArray.Create<byte>(255, 2, 3, 4, 5, 6)), NativeCommands = [new(command, "Special", flags, (byte)id)] }, new() { Id = "enemy", Name = "Enemy", ModelId = 22, MaximumHp = 1000, CurrentHp = 1, Speed = 1, NativeAi = ai, NativeSpecial = new(4, 4, [0, 0, 0], 10) }] }, BattleAtbMode.Normal);
    }
}
