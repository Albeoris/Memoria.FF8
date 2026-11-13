using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class StoryBattleRosterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(11)]
    public void TowerStoryEntryPreservesOriginalSlotsWithAnEligibleFourthPartyMember(int expandedBattleAllyCount)
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.TowerEntry.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, expandedBattleAllyCount: expandedBattleAllyCount);
        world.EnsureField();
        for (int frame = 0; frame < 3000 && !world.HasPendingBattle; frame++)
        {
            PlayerAction input = world.CurrentDialogue?.CanConfirm == true && world.CurrentDialogue.FirstChoice is int first ? new(PlayerActionKind.Choose, first) : new(world.CurrentDialogue?.CanConfirm == true ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
            Assert.True(world.Apply(input).Accepted);
        }

        Assert.True(world.HasPendingBattle);
        NativeCharacterVitals vitals = Assert.IsType<NativeCharacterVitals>(session.State.CharacterVitals);
        Assert.Equal(new byte[] { 1, 0, 5, 3 }, ExpandedBattlePartyRows.Select(vitals));
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        Assert.Equal(29, occurrence.Formation);
        ScriptedBattleOwner owner = Assert.IsType<ScriptedBattleOwner>(occurrence.ScriptedOwner);
        Assert.Equal((314, 8, 98, 1375, 1), (owner.FieldId, owner.ObjectId, owner.ScriptId, owner.Operation, owner.Flags));

        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.False(battle.UsesExpandedRoster);
        Assert.NotNull(battle.NativeGraphics);
        Assert.Equal(new byte[] { 1, 0, 5 }, session.State.CharacterVitals!.CombatParty);
        AssertTowerSlots(battle);

        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
        BattleSession restored = BattleSession.Restore(battle.Capture());
        AssertTowerSlots(restored);
    }

    private static void AssertTowerSlots(BattleSession battle)
    {
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal(29, battle.EncounterId);
        Assert.Equal(new[] { ("party-1", 0), ("party-0", 1), ("party-5", 2) }, snapshot.Combatants.Where(actor => actor.Id.StartsWith("party-", StringComparison.Ordinal)).Select(actor => (actor.Id, actor.NativeParticipant!.ActorIndex)));
        Assert.Equal(3, snapshot.Combatants.Single(actor => actor.Id == "enemy-0").NativeParticipant!.ActorIndex);
        NativeBattleParticipantSnapshot wedge = snapshot.Combatants.Single(actor => actor.Id == "enemy-1").NativeParticipant!;
        Assert.Equal(4, wedge.ActorIndex);
        Assert.True(wedge.Active);
        Assert.True(wedge.Hidden);
        Assert.True(wedge.Suspended);
        NativeBattleParticipantSnapshot elvoret = snapshot.Combatants.Single(actor => actor.Id == "enemy-2").NativeParticipant!;
        Assert.Equal(-1, elvoret.ActorIndex);
        Assert.False(elvoret.Active);
    }
}
