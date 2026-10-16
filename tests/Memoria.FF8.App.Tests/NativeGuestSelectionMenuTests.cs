using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGuestSelectionMenuTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(65539)]
    public void GuestRequestPreservesBackupsAndResetsNormalRowsBeforeSelection(int operand)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals().WithMemberFlags(1, true).WithMemberFlags(5, true);
        NativeCharacterVital[] rows = initial.Records.ToArray();
        rows[0] = rows[0] with { Junction = rows[0].Junction with { GuardianForces = 1 } };
        rows[1] = rows[1] with { Junction = rows[1].Junction with { GuardianForces = 2, Commands = 0x04030201 } };
        initial = initial with { Records = rows, CombatParty = [5, 0, 1] };
        NativeJunctionResult requested = initial.PrepareJunction(operand, [0, 1, 5], 7);
        Assert.Equal(new byte[] { 0, 1, 5, 5, 0, 1 }, requested.Vitals.JunctionPartyBackup);
        Assert.Equal(new byte[] { 0, 1, 5 }, requested.FieldParty);
        Assert.Equal(new byte[] { 5, 0, 1 }, requested.Vitals.CombatParty);
        Assert.Equal(1, requested.Vitals.Records[0].Junction.GuardianForces);
        Assert.Equal(0, requested.Vitals.Records[1].Junction.GuardianForces);
        Assert.Equal(0u, requested.Vitals.Records[1].Junction.Commands);
        Assert.Equal(2, initial.Records[1].Junction.GuardianForces);
        Assert.True(requested.Vitals.DreamEnabled);
        Assert.Equal(8, requested.Vitals.Records[0].CharacterId);
        NativeCharacterVitals active = requested.Vitals.EnterGuestSelectionMenu();
        Assert.False(active.DreamEnabled);
        Assert.Equal(0, active.Records[0].CharacterId);
        Assert.Equal(1, active.Records[0].Junction.GuardianForces);
        NativeJunctionResult mapped = active.CompleteGuestSelectionMenu([0, 5, 1], 7);
        Assert.Equal(new byte[] { 8, 9, 10 }, mapped.FieldParty);
        Assert.Equal(new byte[] { 1, 0, 5 }, mapped.Vitals.CombatParty);
        Assert.Equal(5, mapped.Vitals.FindPhysicalRow(9));
        Assert.Equal(1, mapped.Vitals.FindPhysicalRow(10));
        NativeJunctionResult onlyWard = active.CompleteGuestSelectionMenu([0, 255, 5], 5);
        Assert.Equal(new byte[] { 8, 10, 255 }, onlyWard.FieldParty);
        Assert.Equal(new byte[] { 5, 0, 255 }, onlyWard.Vitals.CombatParty);
        Assert.Equal(requested.Vitals.JunctionPartyBackup, onlyWard.Vitals.JunctionPartyBackup);
        Assert.Throws<InvalidDataException>(() => active.CompleteGuestSelectionMenu([0, 5, 5], 7));
        Assert.Throws<InvalidDataException>(() => active.CompleteGuestSelectionMenu([0, 4, 5], 7));
    }

    [Fact]
    public void Original180Junction22WaitsForExplicitGuestMappingAndRestoresEveryMenuPhase()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field180 is required.");
        string source = TestResources.FixturePath("App.Tests.NativeGuestSelectionMenuTests.1");
        GameState initial = GameSession.Load(source);
        GameSession session = new(initial);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        byte[] fieldBefore = world.Events.Scene.Party.Select(identity => checked((byte)identity)).ToArray();
        byte[] combatBefore = session.CharacterVitals!.CombatParty.ToArray();
        NativeCharacterVitals before = session.CharacterVitals.DeepCopy();
        for (int callback = 0; world.Events.PartyMenu is null && callback < 300; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.NotNull(world.Events.PartyMenu);
        Assert.Equal((180, 0, 10, 269, 22, 7, 3), (world.Events.PartyMenu!.FieldId, world.Events.PartyMenu.ObjectId, world.Events.PartyMenu.ScriptId, world.Events.PartyMenu.Operation, world.Events.PartyMenu.NativeMenuId, (int)world.Events.PartyMenu.NativeEntrySelection, world.Events.PartyMenu.JunctionRawArgument));
        Assert.Equal(NativePartyMenuPhase.Requested, world.Events.PartyMenu.Phase);
        Assert.True(session.CharacterVitals!.DreamEnabled);
        Assert.Equal(8, session.CharacterVitals.Records[0].CharacterId);
        Assert.Equal(fieldBefore.Concat(combatBefore), session.CharacterVitals.JunctionPartyBackup);
        NativeCharacterVitals expectedRequest = before.PrepareJunction(3, fieldBefore.Select(identity => (int)identity).ToArray(), 7).Vitals;
        Assert.Equal(JsonSerializer.SerializeToElement(expectedRequest).GetRawText(), JsonSerializer.SerializeToElement(session.CharacterVitals).GetRawText());
        foreach (int row in Enumerable.Range(1, 7))
        {
            Assert.Equal(0, session.CharacterVitals.Records[row].Junction.GuardianForces);
            Assert.All(session.CharacterVitals.Records[row].Junction.MagicJunctions, value => Assert.Equal(0, value));
        }
        long frame = world.Events.Frame;
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        for (int callback = 0; world.Events.PartyMenu!.Phase != NativePartyMenuPhase.Active && callback < 5; callback++)
        {
            JsonElement next = RestoreAndCompare(world, session, directory);
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(next, world.Events.Capture()));
        }
        Assert.False(session.CharacterVitals.DreamEnabled);
        Assert.Equal(0, session.CharacterVitals.Records[0].CharacterId);
        Assert.Equal(new byte[] { 0, 255, 255 }, world.Events.PartyMenu!.TemporaryCombatParty);
        Assert.Equal(new byte[] { 1, 2, 3, 5 }, world.Events.PartyMenuCandidates.Where(row => row != 255));
        Assert.Equal(3, world.Events.PartyMenu.NativeReturnResult);
        Assert.Equal(frame, world.Events.Frame);
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, 0)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 5)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, 2)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 1)).Accepted);
        Assert.Equal(new byte[] { 0, 5, 1 }, world.Events.PartyMenu.TemporaryCombatParty);
        JsonElement active = world.Events.Capture();
        JsonNode malformed = JsonNode.Parse(active.GetRawText())!;
        malformed["PartyMenu"]!.AsObject().Remove("JunctionRawArgument");
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
        Assert.True(JsonElement.DeepEquals(active, world.Events.Capture()));
        RestoreAndCompare(world, session, directory);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.True(session.CharacterVitals.DreamEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, world.Events.Scene.Party);
        Assert.Equal(5, session.CharacterVitals.FindPhysicalRow(9));
        Assert.Equal(1, session.CharacterVitals.FindPhysicalRow(10));
        Assert.Equal(new byte[] { 1, 0, 5 }, session.CharacterVitals.CombatParty);
        Assert.Equal(fieldBefore.Concat(combatBefore), session.CharacterVitals.JunctionPartyBackup);
        for (int callback = 0; world.Events.PartyMenu is not null && callback < 12; callback++)
        {
            JsonElement next = RestoreAndCompare(world, session, directory);
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(next, world.Events.Capture()));
        }
        Assert.Null(world.Events.PartyMenu);
        Assert.Null(world.Events.LastFault);
        for (int callback = 0; session.State.FieldId == 180 && callback < 100; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Equal(946, session.State.FieldId);
        Assert.Equal(1200, session.State.WorldEntryMemory!.StoryWord);
    }

    private static JsonElement RestoreAndCompare(GameWorld world, GameSession session, string directory)
    {
        GameState state = session.State with { EventState = world.Events.Capture() };
        GameSession restoredSession = new(state);
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        restored.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Null(restored.Events.LastFault);
        return restored.Events.Capture();
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory!.FullName;
    }
}
