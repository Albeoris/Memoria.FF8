using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterNativeDamageRoutingTests
{
    [Theory]
    [InlineData(100, 950)]
    [InlineData(101, 950)]
    [InlineData(1, 1000)]
    public void NativeResolvedDamageProducesRecipientContactsAndNoOriginalHpLoss(int damage, int expectedHp)
    {
        BattleSession battle = Create(damage);
        NativeBattleContactPacket[] contacts = Hit(battle);
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
        Assert.All(battle.EncounterAllocation!.ActorIds, id => Assert.Equal(expectedHp, battle.Combatants.Single(actor => actor.Id == id).CurrentHp));
        Assert.Equal(0, contacts[0].Primary.PresentedAmount);
        Assert.Equal((byte)0, contacts[0].Primary.Sequence);
        Assert.All(contacts.Skip(1), packet => Assert.Equal(damage / 2, packet.Primary.PresentedAmount));
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage" && entry.TargetId == "original");
    }

    [Fact]
    public void NativeAllTargetHitFreezesRecipientsAndIncludesDirectPlusRoutedDamage()
    {
        BattleSession battle = Create(100, allTargets: true);
        NativeBattleContactPacket[] contacts = Hit(battle);
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
        Assert.All(battle.EncounterAllocation!.ActorIds, id => Assert.Equal(850, battle.Combatants.Single(actor => actor.Id == id).CurrentHp));
        Assert.Equal(5, contacts.Length);
        Assert.Equal(new[] { 0, 50, 50, 100, 100 }, contacts.Select(packet => packet.Primary.PresentedAmount).Order().ToArray());
    }

    [Fact]
    public void LethalSharesKeepCopyKoAndDoNotRunOriginalStoryOrRewardOwners()
    {
        BattleSession battle = Create(300, maximumHp: 100);
        _ = Hit(battle);
        Assert.Equal(100, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
        BattleSnapshot snapshot = battle.Capture();
        foreach (string id in battle.EncounterAllocation!.ActorIds)
        {
            Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == id).CurrentHp);
            Assert.Equal(1, snapshot.Combatants.Single(actor => actor.Id == id).NativeStatus!.Saved & 1);
            Assert.Equal(0u, battle.Combatants.Single(actor => actor.Id == id).NativeCombatantFlags & 0x20);
            Assert.Null(battle.GetCombatantDefinition(id).Rewards);
        }
        Assert.Equal(2, battle.Events.Count(entry => entry.Kind == "Defeated"));
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.DoesNotContain(snapshot.QueuedActions, command => command.NativeAction?.ActionId == 264);
    }

    [Fact]
    public void LethalAllTargetContactDoesNotPublishDuplicateCopyDeaths()
    {
        BattleSession battle = Create(100, allTargets: true, maximumHp: 50);
        _ = Hit(battle);
        Assert.Equal(50, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
        Assert.All(battle.EncounterAllocation!.ActorIds, id => Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == id).CurrentHp));
        Assert.Equal(2, battle.Events.Count(entry => entry.Kind == "Defeated"));
    }

    [Fact]
    public void DefeatedCopiesReleaseTheirAllocationsWithoutEndingTheOriginalBattle()
    {
        BattleSession battle = Create(100, maximumHp: 50);
        _ = Hit(battle);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.NativeActionPackets.ActionCount != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        foreach (string id in battle.EncounterAllocation!.ActorIds)
        {
            Assert.False(battle.Combatants.Single(actor => actor.Id == id).IsActive);
            Assert.Single(battle.Events, entry => entry.Kind == "NativeModelRemoved" && entry.ActorId == id);
        }
        Assert.True(battle.Combatants.Single(actor => actor.Id == "original").IsAlive);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Empty(battle.EnemyDefeats);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void TerminalCleanupDoesNotReleasePreviouslyRetiredCopiesAgain()
    {
        BattleSession battle = Create(100, maximumHp: 50);
        _ = Hit(battle);
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.NativeActionPackets.ActionCount != 0); frame++)
        {
            battle.Tick();
        }
        Assert.All(battle.EncounterAllocation!.ActorIds, id => Assert.False(battle.Combatants.Single(actor => actor.Id == id).IsActive));
        int removed = battle.Events.Count(entry => entry.Kind == "NativeModelRemoved");
        Assert.Equal(2, removed);
        typeof(BattleSession).GetField("encounterPolicies", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, new BattleEncounterPolicyState("fixture-generation", ImmutableDictionary<string, EncounterActionCursor>.Empty, TerminalStarted: true));
        MethodInfo cleanup = typeof(BattleSession).GetMethod("CleanupEncounterPoliciesAtTerminal", BindingFlags.Instance | BindingFlags.NonPublic)!;
        cleanup.Invoke(battle, null);
        cleanup.Invoke(battle, null);
        Assert.True(battle.EncounterPolicies!.CleanedUp);
        Assert.Equal(removed, battle.Events.Count(entry => entry.Kind == "NativeModelRemoved"));
        Assert.Empty(battle.EnemyDefeats);
    }

    [Fact]
    public void TerminalCleanupPurgesRetiringCopyCommandsFromTheDetachedCallbackTail()
    {
        BattleSession battle = Create(100);
        string copy = battle.EncounterAllocation!.ActorIds[0];
        BattleAttackCommand copyCommand = new(copy, "party", BattleAbility.EnemyAbility, 1);
        BattleAttackCommand originalTerminal = new("original", "original", BattleAbility.EnemyAbility, 207);
        BattleAttackCommand unrelated = new("party", "original", BattleAbility.Attack, 0);
        List<BattleAttackCommand> tail = [copyCommand, originalTerminal, unrelated];
        typeof(BattleSession).GetField("nativeCallbackTail", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, tail);
        Queue<BattleAttackCommand> actions = (Queue<BattleAttackCommand>)typeof(BattleSession).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        actions.Clear();
        actions.Enqueue(copyCommand);
        actions.Enqueue(originalTerminal);
        typeof(BattleSession).GetField("encounterPolicies", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, new BattleEncounterPolicyState("fixture-generation", ImmutableDictionary<string, EncounterActionCursor>.Empty, TerminalStarted: true));
        typeof(BattleSession).GetMethod("CleanupEncounterPoliciesAtTerminal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);
        Assert.Equal(new[] { originalTerminal, unrelated }, tail);
        Assert.Same(originalTerminal, Assert.Single(battle.QueuedActions, command => ReferenceEquals(command, originalTerminal)));
        Assert.DoesNotContain(battle.QueuedActions, command => command.ActorId == copy);
        Assert.All(battle.EncounterAllocation.ActorIds, id => Assert.False(battle.Combatants.Single(actor => actor.Id == id).IsActive));
    }

    [Fact]
    public void NativeDrainRetainsOneSecondaryHealingContactWhenPrimaryDamageRoutes()
    {
        BattleSession battle = Create(100, drain: true);
        int before = battle.Combatants.Single(actor => actor.Id == "party").CurrentHp;
        NativeBattleContactPacket[] contacts = Hit(battle);
        NativeBattleContactHalf secondary = Assert.Single(contacts, packet => packet.Secondary.ActorIndex != 255).Secondary;
        Assert.Equal((byte)0, secondary.ActorIndex);
        Assert.True(secondary.PresentedAmount > 0);
        Assert.Equal(Math.Min(1000, before + secondary.PresentedAmount), battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Single(battle.Events, entry => entry.Kind == "Healing");
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
    }

    [Fact]
    public void PeriodicPositiveDamageRoutesWithoutCopyingTheOriginalPoisonState()
    {
        BattleSession battle = Create(100, poison: true);
        battle.Tick();
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
        Assert.All(battle.EncounterAllocation!.ActorIds, id =>
        {
            BattleCombatant copy = battle.Combatants.Single(actor => actor.Id == id);
            Assert.Equal(950, copy.CurrentHp);
            Assert.Empty(copy.StatusEffects);
        });
    }

    [Fact]
    public void ExpiredDoomKeepsOriginalTerminalCallbackAndRetiresCopiesOnceAcrossRestore()
    {
        BattleSession battle = Create(100);
        BattleSnapshot snapshot = battle.Capture();
        BattleCombatantDefinition original = snapshot.Definition.Combatants.Single(actor => actor.Id == "original");
        NativeBattleAiDefinition terminal = original.NativeAi! with { Scripts = [[0], [0], [0], [57, 0], [0]] };
        snapshot = snapshot with
        {
            Definition = snapshot.Definition with { NativeFieldBattleMode = 0x200, Combatants = snapshot.Definition.Combatants.Select(actor => actor.Id == "original" ? actor with { NativeAi = terminal } : actor).ToArray() },
            EncounterPolicies = new("fixture-generation", ImmutableDictionary<string, EncounterActionCursor>.Empty)
        };
        int index = Array.FindIndex(snapshot.Combatants, actor => actor.Id == "original");
        NativeBattleStatusSnapshot status = snapshot.Combatants[index].NativeStatus!;
        short[] timers = status.Timers.ToArray();
        timers[10] = 0;
        snapshot.Combatants[index] = snapshot.Combatants[index] with { NativeStatus = status with { Battle = status.Battle | 0x400, Timers = timers } };
        battle = BattleSession.Restore(snapshot);
        battle.Tick();
        Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "original").CurrentHp);
        Assert.All(battle.EncounterAllocation!.ActorIds, id => Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == id).CurrentHp));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 100 && battle.Outcome == BattleOutcome.Active; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.True(battle.EncounterPolicies!.CleanedUp);
        Assert.All(battle.EncounterAllocation.ActorIds, id => Assert.False(battle.Combatants.Single(actor => actor.Id == id).IsActive));
        Assert.Equal(2, battle.Events.Count(entry => entry.Kind == "NativeModelRemoved"));
        Assert.Empty(battle.EnemyDefeats);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        battle.Tick();
        Assert.Equal(2, battle.Events.Count(entry => entry.Kind == "NativeModelRemoved"));
    }

    [Fact]
    public void PeriodicRoutedLethalDamageCanAdmitAnIndependentNativeDeathCounter()
    {
        BattleSnapshot snapshot = Create(100, poison: true, fourthCounter: true).Capture();
        EncounterCompiledDefinition mod = snapshot.Definition.EncounterMod!;
        snapshot = snapshot with
        {
            Definition = snapshot.Definition with
            {
                EncounterMod = mod with { DamageRoutes = mod.DamageRoutes.SetItem("share", new("share", "original", ["fourth"])) },
                Combatants = snapshot.Definition.Combatants.Select(actor => actor.Id == "fourth" ? actor with { NativeCombatantFlags = 0x31 } : actor).ToArray()
            },
            Combatants = snapshot.Combatants.Select(actor => actor.Id == "fourth" ? actor with { CurrentHp = 1, NativeParticipant = actor.NativeParticipant! with { NativeCombatantFlags = 0x31 } } : actor).ToArray()
        };
        BattleSession battle = BattleSession.Restore(snapshot);
        battle.Tick();
        Assert.Equal(1u, battle.Capture().Combatants.Single(actor => actor.Id == "fourth").NativeParticipant!.Variables[0]);
        Assert.Null(battle.ActiveAction?.NativeEventOwner);
        Assert.All(battle.QueuedActions, command => Assert.Null(command.NativeEventOwner));
        for (int frame = 0; frame < 30 && battle.NativeActionPackets.ActionCount == 0; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)4, battle.NativeActionPackets.Headers[0].ReadByte(0));
        Assert.Equal(990, battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
    }

    [Fact]
    public void ExternalOriginalRemovalRetiresCopiesWithoutDuplicateCleanup()
    {
        BattleSession battle = Create(100);
        BattleSnapshot snapshot = battle.Capture() with { EncounterPolicies = new("fixture-generation", ImmutableDictionary<string, EncounterActionCursor>.Empty) };
        int index = Array.FindIndex(snapshot.Combatants, actor => actor.Id == "original");
        NativeBattleParticipantSnapshot participant = snapshot.Combatants[index].NativeParticipant!;
        snapshot.Combatants[index] = snapshot.Combatants[index] with { NativeParticipant = participant with { Active = false, NativeCombatantFlags = participant.NativeCombatantFlags!.Value & ~1u } };
        battle = BattleSession.Restore(snapshot);
        battle.Tick();
        Assert.True(battle.EncounterPolicies!.CleanedUp);
        Assert.All(battle.EncounterAllocation!.ActorIds, id => Assert.False(battle.Combatants.Single(actor => actor.Id == id).IsActive));
        Assert.Equal(2, battle.Events.Count(entry => entry.Kind == "NativeModelRemoved"));
        Assert.Empty(battle.EnemyDefeats);
        battle.Tick();
        Assert.Equal(2, battle.Events.Count(entry => entry.Kind == "NativeModelRemoved"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FourthActorImmediateCounterPreservesNativeAdmissionContactAndSnapshotAcrossSources(bool enemySource)
    {
        BattleSession battle = Create(100, allTargets: true, fourthCounter: true);
        if (enemySource)
        {
            BattleSnapshot initial = battle.Capture();
            initial = initial with { Definition = initial.Definition with { Combatants = initial.Definition.Combatants.Select(actor => actor.Id == "original" ? actor with { NativeAi = actor.NativeAi! with { Scripts = [[0], [4, 205, 12, 0, 0], [0], [0], [0]] } } : actor).ToArray() } };
            initial.Combatants[0] = initial.Combatants[0] with { Atb = 0 };
            int original = Array.FindIndex(initial.Combatants, actor => actor.Id == "original");
            initial.Combatants[original] = initial.Combatants[original] with { Atb = BattleSession.AtbMaximum };
            battle = BattleSession.Restore(initial);
        }
        else
        {
            battle.QueueNativePlayerCommand("party", "original", 8, 1);
        }
        int expectedHeaders = enemySource ? 1 : 2;
        for (int frame = 0; frame < 10 && battle.NativeActionPackets.ActionCount < expectedHeaders; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)expectedHeaders, battle.NativeActionPackets.ActionCount);
        Assert.Equal(enemySource ? 1000 : 990, battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Equal(900, battle.Combatants.Single(actor => actor.Id == "fourth").CurrentHp);
        Assert.All(battle.EncounterAllocation!.ActorIds, id => Assert.Equal(850, battle.Combatants.Single(actor => actor.Id == id).CurrentHp));
        NativeBattleContactPacket countered = Assert.Single(battle.NativeActionPackets.ReadHeaderContacts506BA0(0), packet => packet.Primary.ActorIndex == 4);
        Assert.Equal(enemySource ? (byte)5 : (byte)18, countered.Primary.Sequence);
        BattleSnapshot snapshot = battle.Capture();
        if (!enemySource)
        {
            BattleAttackCommand counter = Assert.Single(snapshot.QueuedActions, command => command.ActorId == "fourth" && command.NativeAiCallbackStage == 4);
            Assert.Equal(new BattleNativeEventOwner("party", 0), counter.NativeEventOwner);
            BattleSnapshot invalid = snapshot with { QueuedActions = snapshot.QueuedActions.Select(command => ReferenceEquals(command, counter) ? command with { NativeEventOwner = new("fourth", 0) } : command).ToArray() };
            Assert.Throws<InvalidDataException>(() => BattleSession.Restore(invalid));
        }
        Assert.Equal(enemySource ? 0u : 1u, snapshot.Combatants.Single(actor => actor.Id == "fourth").NativeParticipant!.Variables[0]);
        if (!enemySource)
        {
            Assert.Equal((ushort)0, snapshot.Combatants.Single(actor => actor.Id == "fourth").NativeParticipant!.ReturnDamage);
        }
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        for (int frame = 0; frame < 80 && (battle.NativeActionPackets.ActionCount != 0 || battle.ActiveAction is not null || battle.QueuedActions.Count != 0 || battle.NativeCommandBanks.BoundCommands.Any()); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal((byte)0, battle.NativeActionPackets.ActionCount);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.False(battle.Combatants.Single(actor => actor.Id == "fourth").IsBusy);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(enemySource ? 0u : 1u, battle.Capture().Combatants.Single(actor => actor.Id == "fourth").NativeParticipant!.Variables[0]);
        BattleSnapshot next = battle.Capture();
        next = next with { Definition = next.Definition with { Combatants = next.Definition.Combatants.Select(actor => actor.Id == "fourth" ? actor with { NativeAi = actor.NativeAi! with { Scripts = [[0], [4, 201, 12, 0, 0], [0], [0], actor.NativeAi!.Scripts[4]] } } : actor).ToArray() }, Combatants = next.Combatants.Select(actor => actor with { Atb = actor.Id == "fourth" ? BattleSession.AtbMaximum : 0 }).ToArray() };
        battle = BattleSession.Restore(next);
        for (int frame = 0; frame < 10 && battle.NativeActionPackets.ActionCount == 0; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)4, battle.NativeActionPackets.Headers[0].ReadByte(0));
        Assert.Equal(enemySource ? 990 : 980, battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
    }

    [Fact]
    public void ReflectedSourcePublishesItsFourthActorCounterBehindTheAcceptedReflection()
    {
        BattleSnapshot snapshot = Create(100, fourthCounter: true).Capture();
        BattleNativeAction incoming = snapshot.Definition.NativeActions.Single(action => action.ActionId == 1) with { AttackType = 2, AttackFlags = 0x10 };
        BattleNativeAction reflected = incoming with { Command = 247, AttackType = 27, Power = 1, Hit = 0, AttackFlags = 0 };
        snapshot = snapshot with { Definition = snapshot.Definition with { NativeActions = [incoming, reflected, snapshot.Definition.NativeActions.Single(action => action.ActionId == 2)] } };
        snapshot.Combatants[0] = snapshot.Combatants[0] with { NativeStatus = snapshot.Combatants[0].NativeStatus! with { Battle = 0x80 } };
        byte seed = Enumerable.Range(0, 256).Select(value => (byte)value).First(value =>
        {
            NativeBattleRandomState.Initialize(value).Draw(out byte draw);
            return (draw & 3) + 3 == 4;
        });
        snapshot = snapshot with { Random = NativeBattleRandomState.Initialize(seed) };
        BattleSession battle = BattleSession.Restore(snapshot);
        battle.QueueNativePlayerCommand("party", "party", 8, 1);
        for (int frame = 0; frame < 30 && battle.NativeActionPackets.ActionCount < 3; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)3, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)247, battle.NativeActionPackets.Headers[1].ReadByte(1));
        Assert.Equal((byte)4, battle.NativeActionPackets.Headers[2].ReadByte(0));
        Assert.Equal(900, battle.Combatants.Single(actor => actor.Id == "fourth").CurrentHp);
        Assert.Equal(990, battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Equal(1u, battle.Capture().Combatants.Single(actor => actor.Id == "fourth").NativeParticipant!.Variables[0]);
        _ = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }

    [Fact]
    public void HistoricalCopyCallbacksDoNotReachReplacementWhenOriginalIsRemoved()
    {
        BattleSnapshot snapshot = Create(100, fourthCounter: true).Capture();
        string oldCopy = snapshot.EncounterAllocation!.ActorIds[0];
        snapshot = snapshot with
        {
            Definition = snapshot.Definition with { Combatants = snapshot.Definition.Combatants.Select(actor => actor.Id == "fourth" ? actor with { NativeAi = actor.NativeAi! with { Scripts = [[59, 0, 5, 29, 0, 0], [0], [0], [0], [0]] } } : actor).ToArray() },
            EncounterPolicies = new("fixture-generation", ImmutableDictionary<string, EncounterActionCursor>.Empty),
            Combatants = snapshot.Combatants.Select(actor => actor with { Atb = 0 }).ToArray()
        };
        NativeBattleCommandBanks banks = snapshot.NativeCommandBanks!;
        (banks, NativeBattleCommandLocation old) = banks.EnqueueCallback(0, 5, 1);
        banks = banks.BindCommand(old, 0, new(oldCopy, oldCopy, BattleAbility.EnemyAbility, 0) { NativeCallback = new(5, 1) });
        BattleSession battle = BattleSession.Restore(snapshot with { NativeCommandBanks = banks });
        for (int frame = 0; frame < 12; frame++)
        {
            battle.Tick();
        }
        BattleSnapshot completed = battle.Capture();
        NativeBattleEnemyAllocation replacement = completed.NativeAllocations.Single(allocation => allocation.Generation == 3);
        Assert.Equal(-1, completed.Combatants.Single(actor => actor.Id == oldCopy).NativeParticipant!.ActorIndex);
        Assert.Equal(0, completed.Combatants.Single(actor => actor.Id == replacement.Id).NativeParticipant!.TurnCount);
        Assert.True(battle.Combatants.Single(actor => actor.Id == replacement.Id).IsActive);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "fourth").IsAlive);
        Assert.True(battle.EncounterPolicies!.CleanedUp);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.DoesNotContain(battle.NativeCommandBanks.BoundCommands, command => command.ActorId == oldCopy);
        _ = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(completed))!);
    }

    private static NativeBattleContactPacket[] Hit(BattleSession battle)
    {
        battle.QueueNativePlayerCommand("party", "original", 8, 1);
        for (int frame = 0; frame < 8 && battle.NativeActionPackets.ActionCount == 0; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[0];
        return Enumerable.Range(header.FirstContactIndex!.Value, header.ReadByte(16)).Select(battle.NativeActionPackets.ReadContact).ToArray();
    }

    private static BattleSession Create(int damage, bool allTargets = false, int maximumHp = 1000, bool drain = false, bool poison = false, bool fourthCounter = false)
    {
        byte power = checked((byte)((damage + 99) / 100));
        byte hit = checked((byte)(power * 100 - damage));
        byte targetFlags = allTargets ? (byte)0x40 : (byte)0x50;
        BattleNativeAction action = new(8, 1, 3, 1)
        {
            MagicId = 0, AttackType = drain ? (byte)1 : (byte)27, Power = drain ? (byte)20 : power, HitCount = 1, TargetFlags = targetFlags, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = 0, StatusAttack = drain ? (byte)200 : (byte)0, Statuses0 = 0, Statuses1 = drain ? 0x8000u : 0u, Hit = drain ? (byte)255 : hit, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = new byte[20].ToImmutableArray(), SupplementalRecord = []
        };
        NativeBattleEnemyLevel level = new(maximumHp, 20, 5, 20, 5, 10, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattleEnemyTemplate template = new(1, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()), NativeCombatantFlags = 0x31 };
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
        BattleNativeAction counter = action with { ActionId = 2, Power = 1, Hit = 90, TargetFlags = 0x50 };
        NativeBattleAiDefinition counterAi = new([[0], [0], [0], [0], [18, 220, 1, 9, 18, 4, 201, 12, 0, 0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 7, 2), 48).ToArray(), 0, []);
        EncounterCompiledDefinition mod = new("fixture.encounter", "Classic", "fixture-generation", "original", 11,
            ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, EncounterCompiledTemplate>.Empty, ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty,
            ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty.Add("share", new("share", "original", ["support", "assault"])), ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty, ImmutableDictionary<string, double>.Empty);
        BattleDefinition definition = new()
        {
            EncounterMod = mod,
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = fourthCounter ? [action, counter] : [action],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = drain ? 500 : 1000, InitialAtb = BattleSession.AtbMaximum, NativeCommands = [new(8, "Hit", targetFlags, 1)] },
                new() { Id = "original", Name = "Original", ModelId = 88, SceneSlot = 0, MaximumHp = maximumHp, CurrentHp = maximumHp, Speed = 10, Level = 1, NativeAi = ai, NativeTemplate = template, StatusEffects = poison ? [new() { Kind = BattleStatusKind.Poison, DurationFrames = 60, TickIntervalFrames = 1, Magnitude = 101 }] : [] },
                .. (fourthCounter ? new BattleCombatantDefinition[] { new() { Id = "fourth", Name = "Fourth", SceneSlot = 1, MaximumHp = 1000, CurrentHp = 1000, NativeAi = counterAi, NativeCombatantFlags = 0x11 } } : [])
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.BeginEncounterAllocation("fixture.summon", "original", "support", "assault");
        battle.BeginEncounterAllocationArrival();
        battle.CompleteEncounterAllocationArrival();
        return battle;
    }
}
