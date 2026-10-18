using Memoria.FF8.Runtime;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativePlayerReactionTests
{
    [Fact]
    public void CounterAndAutoPotionKeepAtbAndPersistInOriginalOrderWithOneReservedItem()
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleSession battle = Create(game, assets, 0x40004, [new(1, 2), new(3, 2)], 100);
        int initialHp = battle.Combatants[0].CurrentHp;
        AdvanceUntilReaction(battle);
        BattleSnapshot saved = battle.Capture();
        Assert.Equal([BattleNativePlayerReaction.Counter, BattleNativePlayerReaction.AutoPotion], saved.QueuedActions.Select(command => command.NativePlayerReaction!.Value).ToArray());
        Assert.True(battle.Combatants[0].Atb > 0);
        Assert.Equal(3, saved.QueuedActions[1].ActionId);
        Assert.Equal(1, saved.NativeItemInventory!.Single(stock => stock.ItemId == 3).Count);
        //483400 commits each reaction's physical action facts before publishing its queued presentation mirror.
        Assert.Equal((byte?)1, saved.Combatants[0].NativeParticipant!.LastHealing);
        Assert.All(saved.QueuedActions, command => Assert.NotNull(command.NativeHeaderIndex));
        NativeBattleActionHeaderSlot counter = saved.NativeActionPackets!.Headers[saved.QueuedActions[0].NativeHeaderIndex!.Value];
        NativeBattleActionHeaderSlot potion = saved.NativeActionPackets.Headers[saved.QueuedActions[1].NativeHeaderIndex!.Value];
        Assert.Equal((byte)1, counter.ReadByte(1));
        Assert.Equal((byte)4, potion.ReadByte(1));
        Assert.Equal((ushort)3, NativeBattleActionGraphics.Word(potion, 4));
        NativeBattleContactPacket counterContact = saved.NativeActionPackets.ReadContact(counter.FirstContactIndex!.Value);
        NativeBattleContactPacket healingContact = saved.NativeActionPackets.ReadContact(potion.FirstContactIndex!.Value);
        Assert.Equal((byte)3, counterContact.Primary.ActorIndex);
        Assert.Equal((byte)0, healingContact.Primary.ActorIndex);
        BattleEvent healing = Assert.Single(battle.Events, item => item.Kind == "Healing");
        Assert.Equal("party", healing.TargetId);
        Assert.True(healing.Amount > 0);
        Assert.Equal(checked((ushort)healing.Amount), healingContact.Primary.Amount6);
        Assert.Equal(1, healingContact.Primary.HitFlags3 & 1);
        BattleEvent received = Assert.Single(battle.Events, item => item.Kind == "Damage" && item.ActorId == "enemy");
        Assert.Equal(initialHp - received.Amount + healing.Amount, battle.Combatants[0].CurrentHp);
        string json = JsonSerializer.Serialize(saved);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(saved.NativeItemInventory, restored.Capture().NativeItemInventory);
        Assert.Equal(battle.Combatants[0].Atb, restored.Combatants[0].Atb);
        int[] committedHp = battle.Combatants.Select(actor => actor.CurrentHp).ToArray();
        for (int frame = 0; frame < 128 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(committedHp, battle.Combatants.Select(actor => actor.CurrentHp));
            Assert.Single(battle.Events, item => item.Kind == "Healing");
            Assert.Equal(saved.NativeItemInventory, battle.Capture().NativeItemInventory);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(1, battle.Capture().NativeItemInventory!.Single(stock => stock.ItemId == 3).Count);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(9, 9)]
    public void AutoPotionUsesTheOriginalLargeDeficitPriorityAndConsumesAtProduction(int available, int expected)
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleSession battle = Create(game, assets, 0x40000, [new(available, 1)], 100);
        AdvanceUntilReaction(battle);
        BattleAttackCommand command = Assert.Single(battle.Capture().QueuedActions);
        Assert.Equal(BattleNativePlayerReaction.AutoPotion, command.NativePlayerReaction);
        Assert.Equal(expected, command.ActionId);
        Assert.DoesNotContain(battle.Capture().NativeItemInventory!, stock => stock.ItemId == available);
    }

    [Fact]
    public void AutoPotionDoesNotFallBackToHiPotionForSmallDeficit()
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleSession battle = Create(game, assets, 0x40000, [new(2, 1)], 1);
        for (int frame = 0; frame < 128 && (frame == 0 || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.Capture().QueuedActions);
        Assert.Contains(battle.Capture().NativeItemInventory!, stock => stock.ItemId == 2 && stock.Count == 1);
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, uint flags, BattleStock[] items, byte level)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0) with { Level = level, NativeStatusPassiveFlags = flags };
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        BattleNativePlayerStatistics stats = rules.Project(Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), growth);
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8 && action.AttackType == 1 && (action.AttackFlags & 3) == 0 && action.HitCount == 1 && action.Power is > 0 and <= 10 && action.Hit >= 100 && action.Statuses0 == 0 && action.Statuses1 == 0);
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeActions = assets.NativeActions.ToArray(),
            Items = items,
            Combatants =
            [
                new() { Id = "party", Name = "Squall", ModelId = 0, IsPlayer = true, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp / 3, InitialAtb = 1234, NativePlayerStatRules = rules, NativeGrowth = growth, NativeDeathCount = 0, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0), Defense = stats.Vitality, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Attack = 10, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
        BattleAttackCommand command = new("enemy", "party", BattleAbility.EnemyAbility, attack.ActionId) { NativeQueuedCommand = 8, NativeAction = attack, NativeTargetMask = 1 };
        return BattleSession.Restore(battle.Capture() with { QueuedActions = [command] });
    }

    private static void AdvanceUntilReaction(BattleSession battle)
    {
        for (int frame = 0; frame < 128 && !battle.QueuedActions.Any(command => command.NativePlayerReaction is not null); frame++)
        {
            battle.Tick();
        }
        Assert.Contains(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Contains(battle.Events, item => item.Kind == "Damage" && item.ActorId == "enemy");
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return directory;
    }
}



