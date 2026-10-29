using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAngeloSupportTests
{
    [Theory]
    [InlineData(1, BattleNativePlayerReaction.AngeloRush, 11, 8)]
    [InlineData(3, BattleNativePlayerReaction.AngeloRecover, 12, 1)]
    public void DamageCallbackUsesLearnedPriorityAndTheOriginalAttackerOrSelfMask(byte learned, BattleNativePlayerReaction reaction, int id, ushort mask)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, learned, false);
        AdvanceUntilSupport(battle);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(reaction, queued.NativePlayerReaction);
        Assert.Equal((byte)240, queued.NativeQueuedCommand);
        Assert.Equal(id, queued.ActionId);
        Assert.Equal(mask, queued.NativeTargetMask);
        Assert.True(battle.Combatants[0].Atb > 0);
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(snapshot, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!).Capture()));
    }

    [Fact]
    public void LethalReverseCommitsRevivalBeforeItsPreparedPresentationAndDoesNotReviveTwiceAfterRestore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 4, true);
        AdvanceUntilSupport(battle);
        //483400 prepares48D200/48E830 synchronously; the queued command mirrors facts already committed by the death callback.
        Assert.True(battle.Combatants[0].IsAlive);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(BattleNativePlayerReaction.AngeloReverse, queued.NativePlayerReaction);
        Assert.Equal((ushort)0x4001, queued.NativeTargetMask);
        Assert.NotNull(queued.NativeHeaderIndex);
        BattleSnapshot prepared = battle.Capture();
        NativeBattleActionHeaderSlot header = prepared.NativeActionPackets!.Headers[queued.NativeHeaderIndex!.Value];
        Assert.Equal((byte)240, header.ReadByte(1));
        Assert.Equal((ushort)13, NativeBattleActionGraphics.Word(header, 4));
        NativeBattleContactPacket contact = prepared.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        int revivedHp = battle.Combatants[0].CurrentHp;
        Assert.Equal(checked((ushort)revivedHp), contact.Primary.Amount6);
        Assert.Equal(1, contact.Primary.HitFlags3 & 1);
        BattleEvent healing = Assert.Single(battle.Events, item => item.Kind == "Healing");
        Assert.Equal("rinoa", healing.TargetId);
        Assert.Equal(revivedHp, healing.Amount);
        string json = JsonSerializer.Serialize(prepared);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(contact.Encode(), restored.Capture().NativeActionPackets!.ReadContact(header.FirstContactIndex.Value).Encode());
        for (int frame = 0; frame < 128 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(revivedHp, battle.Combatants[0].CurrentHp);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.Single(battle.Events, item => item.Kind == "Healing");
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.True(battle.Combatants[0].IsAlive);
        Assert.Equal(revivedHp, battle.Combatants[0].CurrentHp);
        Assert.Single(battle.Events, item => item.Kind == "Healing");
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void SavedAngeloProhibitionDoesNotConsumeAnInvocationRoll()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 3, false, 0x10);
        NativeBattleRandomState random = battle.Capture().Random!;
        for (int frame = 0; frame < 128 && (frame == 0 || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(random, battle.Capture().Random);
    }

    private static void AdvanceUntilSupport(BattleSession battle)
    {
        for (int frame = 0; frame < 128 && !battle.QueuedActions.Any(command => command.NativePlayerReaction is not null); frame++)
        {
            battle.Tick();
        }
        Assert.Contains(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Contains(battle.Events, item => item.Kind == "Damage" && item.ActorId == "enemy");
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, byte learned, bool lethal, byte savedFlags = 0)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0) with { NativeStatusPassiveFlags = 0 };
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        ushort maximumHp = rules.Project(vitals.Records[0].MagicInventory, growth).MaximumHp;
        // Explicit fixed-damage fixture isolates the callback's RNG draw from physical hit/critical/formula draws.
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 1000, AttackType = lethal ? (byte)27 : (byte)35, Power = 1, Hit = 0, HitCount = 1, AttackFlags = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeFieldBattleRules = 0,
            NativeActions = [.. assets.NativeActions, attack],
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            GuardianForces = [new(0, 100, 100) { KillCount = 0, DeathCount = 0 }],
            NativeSavedBattleFlags = savedFlags,
            NativeSavedBattleFlagsKnownMask = 255,
            LimitProgress = game.ReadNewGameLimitProgress() with { AngeloFlags = learned },
            Combatants =
            [
                new() { Id = "rinoa", Name = "Rinoa", ModelId = 4, IsPlayer = true, MaximumHp = maximumHp, CurrentHp = 10, InitialAtb = 1234, NativePlayerStatRules = rules, NativeGrowth = growth, NativeDeathCount = 0, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
        BattleAttackCommand command = new("enemy", "rinoa", BattleAbility.EnemyAbility, attack.ActionId) { NativeQueuedCommand = 8, NativeAction = attack, NativeTargetMask = 1 };
        NativeBattleRandomState random = new(Enumerable.Repeat((byte)1, 8).ToImmutableArray(), 0);
        return BattleSession.Restore(battle.Capture() with { QueuedActions = [command], Random = random });
    }
}
