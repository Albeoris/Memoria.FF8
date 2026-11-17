using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleMaximumHpObservationTests
{
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 4)]
    public void AdmittedAllyHasTheSameMaximumHpFactLiveAndAfterJsonRestore(bool expanded, int count)
    {
        BattleSession battle = CreateBattle(expanded, count);
        BattleCombatant selected = battle.Combatants[count - 1];
        Assert.Equal(count - 1, selected.Slot!.Value.Position);
        if (expanded)
        {
            Assert.Equal(new byte[] { 0 }, battle.ReadDefinition().MagicVitals!.GetBattlePartyRows(3));
            Assert.Equal(NativeBattleSlots.Unassigned, battle.Capture().Combatants[count - 1].NativeParticipant!.ActorIndex);
        }
        BattleLiveMaximumHp maximum = Assert.Single(battle.ObserveLiveState().MaximumHitPoints, fact => fact.Eligible);
        Assert.Equal(new BattleLiveMaximumHp(selected.Id, selected.MagicRecordId, true, true), maximum);
        AssertAgreement(battle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KoKeepsEligibilityWhileInactiveSuspendedAndUnownedActorsDoNot(bool expanded)
    {
        BattleSession battle = CreateBattle(expanded, 2, knockedOut: true);
        Assert.False(battle.Combatants[1].IsAlive);
        Assert.True(battle.ObserveLiveState().MaximumHitPoints[1].Eligible);
        AssertAgreement(battle);

        battle.Combatants[1].Suspended = true;
        Assert.False(battle.ObserveLiveState().MaximumHitPoints[1].Eligible);
        AssertAgreement(battle);
        battle.Combatants[1].IsActive = false;
        battle.Combatants[1].Suspended = false;
        Assert.False(battle.ObserveLiveState().MaximumHitPoints[1].Eligible);
        AssertAgreement(battle);

        BattleSession unowned = CreateBattle(expanded, 2, ownsRecord: false);
        Assert.Equal(new BattleLiveMaximumHp("ally-1", null, true, false), unowned.ObserveLiveState().MaximumHitPoints[1]);
        AssertAgreement(unowned);
    }

    [Fact]
    public void ExpandedMaximumPublishesOnceFromEitherAcceptedObservation()
    {
        BattleSession battle = CreateBattle(true, 4);
        BattleEncounterOccurrence occurrence = new("maximum-hp-encounter", 1, new string('a', 64), SceneKind.Field, 74, 0, new string('b', 64), null);
        GameState state = new() { Scene = SceneKind.Field, FieldId = 74, PendingBattleEncounter = occurrence };
        GameSession live = new(state);
        live.SetLiveBattle(battle);
        live.SetLiveBattle(battle);
        OriginalAchievementActionEvent fact = Assert.Single(live.State.PendingOriginalAchievementActions);
        Assert.Equal(OriginalAchievementActionKind.MaximumHitPoints, fact.Kind);

        GameSession restored = new(state);
        BattleSnapshot saved = Deserialize(battle.Capture());
        restored.SetBattle(saved);
        restored.SetLiveBattle(BattleSession.Restore(saved));
        OriginalAchievementActionEvent restoredFact = Assert.Single(restored.State.PendingOriginalAchievementActions);
        Assert.Equal(fact.EventId, restoredFact.EventId);
        Assert.Equal(fact.Kind, restoredFact.Kind);
    }

    [Fact]
    public void TransformationExcludesHumanStatisticAndMissingSavedMaximumRemainsUnknown()
    {
        BattleSession battle = CreateBattle(true, 2);
        BattleCombatant actor = battle.Combatants[1];
        BattleSnapshot source = battle.Capture();
        BattleCombatantSnapshot savedActor = source.Combatants[1];
        BattleCombatantSnapshot[] withoutMaximum = source.Combatants.ToArray();
        withoutMaximum[1] = savedActor with { NativeParticipant = savedActor.NativeParticipant! with { MaximumHitPoints = null } };
        BattleSnapshot unknown = Deserialize(source with { Combatants = withoutMaximum });
        Assert.False(BattleLiveObservation.FromSnapshot(unknown).MaximumHitPoints[1].Eligible);

        actor.GuardianFormVitals = new(() => new BattleGuardianForceVital(2, 9999, 9999), _ => { }, new(), null!);
        Assert.Equal(9999, actor.MaximumHp);
        Assert.False(battle.ObserveLiveState().MaximumHitPoints[1].Eligible);
        Assert.Null(battle.Capture().Combatants[1].NativeParticipant!.MaximumHitPoints);
        Assert.False(BattleLiveObservation.FromSnapshot(Deserialize(battle.Capture())).MaximumHitPoints[1].Eligible);
    }

    private static void AssertAgreement(BattleSession battle)
    {
        BattleSnapshot saved = Deserialize(battle.Capture());
        BattleLiveMaximumHp[] expected = battle.ObserveLiveState().MaximumHitPoints.ToArray();
        Assert.Equal(expected, BattleLiveObservation.FromSnapshot(saved).MaximumHitPoints.ToArray());
        Assert.Equal(expected, BattleSession.Restore(saved).ObserveLiveState().MaximumHitPoints.ToArray());
    }

    private static BattleSnapshot Deserialize(BattleSnapshot snapshot)
    {
        return JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!;
    }

    private static BattleSession CreateBattle(bool expanded, int count, bool knockedOut = false, bool ownsRecord = true)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create().WithExtendedRoster(true);
        if (expanded)
        {
            vitals = vitals with { CombatParty = [0, 255, 255], AdditionalCombatParty = [8, 9, 10] };
        }
        byte[] character = new byte[36];
        for (int offset = 12; offset < character.Length; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 40;
            character[offset + 3] = 1;
        }
        ImmutableArray<ImmutableArray<byte>> spells = Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray();
        ImmutableArray<ImmutableArray<byte>> passives = Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray();
        BattleCombatantDefinition[] party = Enumerable.Range(0, count).Select(index =>
        {
            bool candidate = index == count - 1;
            int row = expanded && index > 0 ? 7 + index : index;
            return new BattleCombatantDefinition
            {
                Id = $"ally-{index}",
                Name = $"Ally {index + 1}",
                IsPlayer = true,
                MagicRecordId = candidate && !ownsRecord ? null : row,
                MaximumHp = candidate ? 9999 : 100,
                CurrentHp = candidate && knockedOut ? 0 : 100,
                NativeStatus0 = candidate && knockedOut ? (ushort)1 : (ushort)0,
                NativePlayerStatRules = candidate ? new(row, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), spells, passives, new byte[20].ToImmutableArray(), 0, vitals.Definitions) { NativeStatusPassiveFlags = 0 } : null,
                NativeGrowth = candidate ? new(9999, new byte[6].ToImmutableArray()) : null
            };
        }).ToArray();
        BattleSession battle = new(new BattleDefinition { EncounterId = 1, MagicVitals = vitals, Combatants = [.. party, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = 100 }] }, BattleAtbMode.TurnBased);
        battle.Combatants[count - 1].ReprojectNativePlayerStatistics();
        return battle;
    }
}
