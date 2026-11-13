using System.Text.Json;
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
        if (expandedBattleAllyCount == 11)
        {
            AssertFullExpandedRosterAvailable(vitals);
        }
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

        BattleSession restored = RestoreAndAdvance(world, battle);
        AssertTowerSlots(restored);
    }

    [Fact]
    public void MagicalLampEntryKeepsNativeSlots()
    {
        string path = TestResources.FixturePath("menu-battle-field-entry");
        string directory = TestResources.GameDirectory;
        Exception? originalCallback = EnterMagicalLampBattle(path, directory, 3);
        Exception? expandedCallback = EnterMagicalLampBattle(path, directory, 11);
        Assert.Equal(originalCallback?.GetType(), expandedCallback?.GetType());
        Assert.Equal(originalCallback?.Message, expandedCallback?.Message);
        if (originalCallback is not null)
        {
            Assert.IsType<NotSupportedException>(originalCallback);
            Assert.Contains("Native504060 completed blend requires its503300 captured pose", originalCallback.Message);
        }
    }

    private static Exception? EnterMagicalLampBattle(string path, string directory, int battleAllyCount)
    {
        GameSession session = new(GameSession.Load(path));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = battleAllyCount });
        if (battleAllyCount == 11)
        {
            AssertFullExpandedRosterAvailable(Assert.IsType<NativeCharacterVitals>(session.State.CharacterVitals));
        }
        byte[] combatParty = session.State.CharacterVitals!.CombatParty.ToArray();

        Assert.True(world.Apply(new(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        Assert.Equal((ushort)811, world.Events.PendingMenuBattle!.Formation);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.Equal(811, battle.EncounterId);
        Assert.False(battle.UsesExpandedRoster);
        Assert.NotNull(battle.NativeGraphics);
        Assert.Equal(combatParty, session.State.CharacterVitals!.CombatParty);
        BattleSession restored = Restore(battle);
        BattleCombatant[] allies = restored.Combatants.Where(actor => actor.IsPlayer).ToArray();
        Assert.Equal(combatParty.Select(row => $"party-{row}"), allies.Select(actor => actor.Id));
        Assert.Equal(new[] { 0, 1, 2 }, allies.Select(actor => actor.EncodeNativeSlot()));
        Assert.Equal(3, Assert.Single(restored.Combatants, actor => !actor.IsPlayer && actor.IsActive).EncodeNativeSlot());
        return Record.Exception(() => world.ApplyBattleInput(new(BattleInputKind.Tick)));
    }

    private static void AssertFullExpandedRosterAvailable(NativeCharacterVitals vitals)
    {
        Assert.False(vitals.ExtendedRosterEnabled);
        byte[] available = Assert.IsType<byte[]>(ExpandedBattlePartyRows.Select(vitals, 11));
        Assert.Equal(Enumerable.Range(0, 11).Select(row => (byte)row), available.Order());
    }

    private static BattleSession RestoreAndAdvance(GameWorld world, BattleSession battle)
    {
        BattleSession restored = Restore(battle);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.Tick();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(restored.Capture())));
        return restored;
    }

    private static BattleSession Restore(BattleSession battle)
    {
        JsonElement snapshot = JsonSerializer.SerializeToElement(battle.Capture());
        BattleSession restored = BattleSession.Restore(snapshot.Deserialize<BattleSnapshot>()!);
        Assert.True(JsonElement.DeepEquals(snapshot, JsonSerializer.SerializeToElement(restored.Capture())));
        return restored;
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
