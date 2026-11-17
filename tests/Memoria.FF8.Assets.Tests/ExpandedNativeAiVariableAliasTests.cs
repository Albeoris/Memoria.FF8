using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedNativeAiVariableAliasTests
{
    [Theory]
    [InlineData(3, 0, false)]
    [InlineData(4, 0, false)]
    [InlineData(3, 2, true)]
    [InlineData(4, 2, true)]
    public void Formation105Model71ClearsOnlyTheOriginalResistanceDwordAndContinuesAfterRestore(int allyCount, byte seed, bool expectContact)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(105);
        BattleEnemyPlacement source = Assert.Single(formation.Enemies, placement => placement.ModelId == 71);
        Assert.Equal(formation.Enemies[0].Slot, source.Slot);
        byte[] partyLevels = Enumerable.Repeat((byte)7, allyCount).ToArray();
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(seed);
        List<BattleCombatantDefinition> actors = Enumerable.Range(0, allyCount).Select(row => new BattleCombatantDefinition
        {
            Id = $"party-{row}", Name = $"Party {row}", IsPlayer = true, ModelId = row, Level = partyLevels[row],
            MaximumHp = 10_000, CurrentHp = 10_000, Speed = 1, NativeStatusResistances = ResistanceMarker(row)
        }).ToList();
        foreach (BattleEnemyPlacement placement in formation.Slots.Where(placement => placement.NativeModelId != 255 && (placement.Enabled || placement.ModelId != 0)))
        {
            byte sceneLevel = checked((byte)placement.Level);
            int draws = placement.Enabled ? BattleEnemyNativeStats.LevelRandomByteCount(sceneLevel) : 0;
            byte first = 0;
            byte? second = null;
            if (draws > 0)
            {
                random = random.Draw(out first);
            }
            if (draws > 1)
            {
                random = random.Draw(out byte roll);
                second = roll;
            }
            int level = !placement.Enabled ? 0 : allyCount == 3
                ? NativeBattleEnemyLevelRules.Resolve(sceneLevel, partyLevels, first, second)
                : NativeBattleEnemyLevelRules.ResolveExpanded(sceneLevel, partyLevels, first, second);
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(level);
            NativeBattleAiDefinition ai = assets.ReadEnemyAi(placement.ModelId, level, includeDeathLifecycle: allyCount > 3);
            if (placement.Slot == source.Slot)
            {
                Assert.Equal(new byte[] { 14, 96, 0 }, ai.Scripts[0][31..34]);
            }
            actors.Add(new()
            {
                Id = $"enemy-{placement.Slot}", Name = assets.ReadEnemyName(placement.ModelId), ModelId = placement.ModelId, SceneSlot = placement.Slot,
                InitialActive = placement.Enabled, InitialHidden = placement.Hidden, InitialSuspended = placement.InitiallySuspended, InitialUntargetable = placement.Untargetable,
                Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit,
                Speed = Math.Max(1, stats.Speed), Evasion = stats.Evasion, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(),
                InitialAtb = placement.Slot == source.Slot ? BattleSession.AtbMaximum - 80 : 0,
                NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(placement.ModelId), NativeAi = ai, NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, sceneLevel)
            });
        }

        // This component supplies synthetic party statistics and no graphics host.
        BattleSession battle = new(new BattleDefinition
        {
            Name = "Installed formation 105 AI", EncounterId = formation.Id, StageId = formation.StageId,
            UsesOriginalEnemySlots = allyCount > 3,
            NativeSeed = seed, InitialRandom = random, PersistentAiVariables = new uint[8], Combatants = actors.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(), NativeActions = assets.NativeActions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0), NativeMessageSpeed = 1, NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages()
        }, BattleAtbMode.Normal);
        battle.Tick();
        BattleSnapshot initialized = battle.Capture();
        for (int row = 0; row < allyCount; row++)
        {
            byte[] expected = ResistanceMarker(row);
            if (row == 0)
            {
                Array.Clear(expected, 20, 4);
            }
            Assert.Equal(expected, initialized.Combatants[row].NativeStatus!.Resistances);
        }

        string enemyId = $"enemy-{source.Slot}";
        BattleSnapshot checkpoint = expectContact ? AdvanceUntil(battle, enemyId, started: true) : initialized;
        if (!expectContact)
        {
            Assert.Equal((byte)0, Assert.Single(checkpoint.Combatants, actor => actor.Id == enemyId).NativeParticipant!.TurnCount);
        }
        BattleSession restored = BattleSession.Restore(checkpoint);
        if (expectContact && allyCount == 3)
        {
            // The physical path calculates contacts before publishing its queued header.
            BattleActionHeaderReference header = Assert.IsType<BattleActionHeaderReference>(checkpoint.ReadFixturePresentation().NativeActionPackets!.ActiveHeader);
            NativeBattleContactPacket[] published = checkpoint.ReadFixturePresentation().NativeActionPackets!.ReadHeaderContacts(header).ToArray();
            Assert.NotEmpty(published);
            Assert.All(published, contact => Assert.IsAssignableFrom<BattlePartyModelOwner>(contact.Primary.Recipient));
            Assert.Equal(JsonSerializer.Serialize(published), JsonSerializer.Serialize(restored.Capture().ReadFixturePresentation().NativeActionPackets!.ReadHeaderContacts(header).ToArray()));
        }
        int priorEvents = battle.Events.Count;
        BattleSnapshot continued = AdvanceUntil(battle, enemyId, started: false);
        BattleSnapshot resumed = AdvanceUntil(restored, enemyId, started: false);

        BattleEvent[] enemyEvents = battle.Events.Skip(priorEvents).Where(entry => entry.ActorId == enemyId).ToArray();
        BattleEvent[] contacts = enemyEvents.Where(entry => entry.TargetId is not null && entry.Kind is "Damage" or "Miss" or "EffectContact" or "Healing").ToArray();
        if (expectContact && allyCount > 3)
        {
            Assert.NotEmpty(contacts);
        }
        else if (!expectContact)
        {
            Assert.Empty(contacts);
            Assert.DoesNotContain(enemyEvents, entry => entry.Kind == "Queued");
        }
        Assert.Equal(enemyEvents, restored.Events.Skip(priorEvents).Where(entry => entry.ActorId == enemyId));
        Assert.Equal(JsonSerializer.Serialize(continued), JsonSerializer.Serialize(resumed));
    }

    private static byte[] ResistanceMarker(int row)
    {
        return Enumerable.Repeat(checked((byte)(90 + row)), 40).ToArray();
    }

    private static BattleSnapshot AdvanceUntil(BattleSession battle, string enemyId, bool started)
    {
        for (int frame = 0; frame < 600; frame++)
        {
            bool reached = started
                ? battle.ActiveAction?.ActorId == enemyId || battle.QueuedActions.Any(command => command.ActorId == enemyId && command.NativeAiCallbackStage == 1)
                : battle.ActiveAction is null && battle.QueuedActions.Count == 0;
            if (reached)
            {
                BattleSnapshot snapshot = battle.Capture();
                BattleCombatantSnapshot enemy = Assert.Single(snapshot.Combatants, actor => actor.Id == enemyId);
                if (enemy.NativeParticipant!.TurnCount > 0)
                {
                    return snapshot;
                }
            }
            battle.Tick();
        }
        BattleCombatant stalled = Assert.Single(battle.Combatants, actor => actor.Id == enemyId);
        BattleCombatantSnapshot state = Assert.Single(battle.Capture().Combatants, actor => actor.Id == enemyId);
        throw new InvalidOperationException($"Model 71 did not reach the expected action boundary within 600 callbacks. Turns {state.NativeParticipant!.TurnCount}, speed {stalled.Speed}, ATB {stalled.Atb}, ready {stalled.IsReady}, busy {stalled.IsBusy}, suspended {stalled.Suspended}, outcome {battle.Outcome}, active action {battle.ActiveAction?.ActorId}, queued {battle.QueuedActions.Count}.");
    }
}
