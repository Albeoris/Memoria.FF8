using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAngeloSupportTests
{
    [Theory]
    [InlineData(1, false, 11, 91, "enemy")]
    [InlineData(3, false, 12, 93, "rinoa")]
    [InlineData(4, true, 13, 94, "rinoa")]
    public void ExpandedDamageQueuesOriginalAngeloReactionAndRestoresBeforeItsContact(byte learned, bool lethal, ushort row, ushort effect, string recipient)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, learned, lethal, expanded: true);
        AdvanceUntilSupport(battle);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Equal("rinoa", queued.ActorId);
        Assert.Equal(recipient, queued.TargetId);
        Assert.Equal((byte)240, queued.NativeQueuedCommand);
        Assert.Equal(row, queued.NativeAction!.ActionId);
        Assert.Equal(effect, queued.NativeAction.MagicId);
        Assert.Equal(0, queued.NativeTargetMask);
        if (lethal)
        {
            Assert.False(battle.Combatants[0].IsAlive);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
        }
        AdvanceRestoredSupport(battle, queued.NativeAction.Name!, recipient);
        Assert.True(battle.Combatants[0].IsAlive);
        if (row == 12)
        {
            Assert.Equal(9 + 10 * battle.Combatants[0].MaximumHp / 16, battle.Combatants[0].CurrentHp);
        }
    }

    [Fact]
    public void ExpandedSearchUsesOriginalClockAndBothRandomOwnersToGrantOneItem()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession initial = Create(game, assets, 8, false, expanded: true, periodic: true);
        BattleSnapshot boundary = initial.Capture();
        BattleSession battle = BattleSession.Restore(boundary with { NativeSupport = boundary.NativeSupport! with { Cooldown = 0 } });
        battle.Tick();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Equal(BattleNativePlayerReaction.AngeloPeriodic, queued.NativePlayerReaction);
        Assert.Equal("ally-0", queued.ActorId);
        Assert.Equal("rinoa", queued.TargetId);
        Assert.Equal(92, queued.NativeAction!.MagicId);
        NativeBattleRandomState expected = boundary.Random!.Draw(out _);
        Assert.Equal(expected.Indexes, battle.Capture().Random!.Indexes);
        Assert.Equal(boundary.NativeCrtRandom, battle.Capture().NativeCrtRandom);
        AdvanceRestoredSupport(battle, queued.NativeAction.Name!, "rinoa");
        BattleSnapshot completed = battle.Capture();
        Assert.Equal(boundary.NativeCrtRandom!.Draw(out _), completed.NativeCrtRandom);
        Assert.Equal(expected.Draw(out _).Indexes, completed.Random!.Indexes);
        Assert.Contains(completed.NativeItemInventory!, item => item is { ItemId: 1, Count: 1 });
        Assert.Single(battle.Events, entry => entry.Kind == "NativeText" && entry.TargetId == "rinoa");
    }

    [Theory]
    [InlineData(2, 1, 12, "ally-0")]
    [InlineData(4, 41, 13, "ally-1")]
    public void ExpandedPeriodicRecoverySelectsAnEligibleAllyWithOriginalRejectionSampling(byte learned, byte phase, ushort actionId, string recipient)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = Create(game, assets, learned, false, expanded: true, periodic: true).Capture().Definition;
        int slot = Array.FindIndex(definition.Combatants, actor => actor.Id == recipient);
        definition.Combatants[slot] = definition.Combatants[slot] with { CurrentHp = actionId == 12 ? 1 : 0, NativeStatus0 = actionId == 12 ? (ushort)0x300 : (ushort)1 };
        BattleSession initial = new(definition with { InitialRandom = NativeBattleRandomState.ForecastPhase(phase) }, BattleAtbMode.Normal);
        BattleSnapshot boundary = initial.Capture();
        BattleSession battle = BattleSession.Restore(boundary with { NativeSupport = boundary.NativeSupport! with { Cooldown = 0 } });
        NativeBattleRandomState expected = boundary.Random!.Draw(out _);
        byte roll;
        do
        {
            expected = expected.Draw(out roll);
        }
        while (roll % 4 != slot && (actionId != 12 || roll % 4 != 3));
        battle.Tick();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Equal(BattleNativePlayerReaction.AngeloPeriodic, queued.NativePlayerReaction);
        Assert.Equal("ally-0", queued.ActorId);
        Assert.Equal(recipient, queued.TargetId);
        Assert.Equal(actionId, queued.NativeAction!.ActionId);
        Assert.Equal(expected.Indexes, battle.Capture().Random!.Indexes);
        AdvanceRestoredSupport(battle, queued.NativeAction.Name!, recipient);
        Assert.Equal(actionId == 12 ? 63 : 12, battle.Combatants[slot].CurrentHp);
    }

    [Theory]
    [InlineData(3, false, false)]
    [InlineData(4, true, false)]
    [InlineData(8, false, true)]
    public void ExpandedAngeloWithNoAcquiredGuardianForceDoesNotQueueOrConsumeChance(byte learned, bool lethal, bool periodic)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, learned, lethal, expanded: true, periodic: periodic, acquired: false);
        BattleSnapshot initial = battle.Capture();
        if (periodic)
        {
            battle = BattleSession.Restore(initial with { NativeSupport = initial.NativeSupport! with { Cooldown = 0 } });
            battle.Tick();
        }
        else
        {
            for (int frame = 0; frame < 128 && !battle.Events.Any(entry => entry is { Kind: "Damage", ActorId: "enemy" }); frame++)
            {
                battle.Tick();
            }
            Assert.Contains(battle.Events, entry => entry is { Kind: "Damage", ActorId: "enemy" });
        }
        Assert.DoesNotContain(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Equal(initial.Random!.Indexes, battle.Capture().Random!.Indexes);
    }

    private static void AdvanceRestoredSupport(BattleSession battle, string name, string recipient)
    {
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
        for (int frame = 0; frame < 128 && !battle.Events.Any(entry => entry.Kind == "EffectContact" && entry.Message == name); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(recipient, Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.Message == name).TargetId);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

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
        NativeBattleActionHeaderSlot header = prepared.NativeActionPackets!.ReadHeader(new BattleActionHeaderReference(queued.NativeHeaderIndex!.Value));
        Assert.Equal((byte)240, header.Command);
        Assert.Equal((ushort)13, header.Action);
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

    private static BattleSession Create(GameAssets game, BattleAssets assets, byte learned, bool lethal, byte savedFlags = 0, bool expanded = false, bool periodic = false, bool acquired = true)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0) with { NativeStatusPassiveFlags = 0 };
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        ushort maximumHp = rules.Project(vitals.Records[0].MagicInventory, growth).MaximumHp;
        // Explicit fixed-damage fixture isolates the callback's RNG draw from physical hit/critical/formula draws.
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 1000, AttackType = lethal ? (byte)27 : (byte)35, Power = 1, Hit = 0, HitCount = 1, AttackFlags = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        BattleCombatantDefinition rinoa = new() { Id = "rinoa", Name = "Rinoa", ModelId = 4, IsPlayer = true, MaximumHp = maximumHp, CurrentHp = 10, InitialAtb = 1234, NativePlayerStatRules = rules, NativeGrowth = growth, NativeDeathCount = 0, Speed = 1, NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition[] actors = [rinoa, enemy];
        if (expanded)
        {
            enemy = enemy with { NativeAi = assets.ReadEnemyAi(4, 1) with { Scripts = [[0], [0], [0], [0], [0]], SequenceTracks = [] }, InitialSuspended = periodic };
            BattleCombatantDefinition[] allies = Enumerable.Range(0, 3).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, ModelId = index, Speed = 1 }).ToArray();
            BattleCombatantDefinition[] extraEnemies = Enumerable.Range(0, 5).Select(index => enemy with { Id = $"enemy-{index}", InitialSuspended = true }).ToArray();
            actors = periodic ? [.. allies, rinoa, enemy, .. extraEnemies] : [rinoa, .. allies, enemy, .. extraEnemies];
        }
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeFieldBattleRules = 0,
            NativeActions = [.. assets.NativeActions, attack],
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            GuardianForces = [new(0, 100, 100, acquired) { KillCount = 0, DeathCount = 0 }],
            NativeSavedBattleFlags = savedFlags,
            NativeSavedBattleFlagsKnownMask = 255,
            LimitProgress = game.ReadNewGameLimitProgress() with { AngeloFlags = learned },
            UsesExpandedRoster = expanded,
            NativeStatusRules = expanded ? assets.ReadNativeStatusRules(0) : null,
            NativeSupportRules = periodic ? assets.ReadNativeSupportRules() : null,
            NativeCrtRandom = periodic ? NativeCrtRandomState.FromColdProcess() : null,
            NativeItemNames = periodic ? Enumerable.Range(0, 256).Select(index => $"Item {index}").ToArray() : [],
            NativeGlobalMessages = periodic ? assets.ReadNativeGlobalBattleMessages() : [],
            NativeMessageSpeed = periodic ? (byte)0 : null,
            InitialRandom = expanded ? NativeBattleRandomState.ForecastPhase(1) : null,
            Combatants = actors
        }, periodic ? BattleAtbMode.Normal : BattleAtbMode.TurnBased);
        if (periodic)
        {
            return battle;
        }
        BattleAttackCommand command = new("enemy", "rinoa", BattleAbility.EnemyAbility, attack.ActionId) { NativeQueuedCommand = 8, NativeAction = attack, NativeTargetMask = expanded ? (ushort)0 : (ushort)1, AiRecipients = expanded ? new(["rinoa"]) : null };
        NativeBattleRandomState random = new(Enumerable.Repeat((byte)1, 8).ToImmutableArray(), 0);
        return BattleSession.Restore(battle.Capture() with { QueuedActions = [command], Random = random });
    }
}
