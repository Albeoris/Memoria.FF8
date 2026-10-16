using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeEndingPartyLoadTests
{
    [Fact]
    public void Original974To979TransitionLoadsWithoutAnUncalledReserveProducer()
    {
        DirectoryInfo? root = new(TestResources.FixtureDirectory);
        string? source = root is null ? null : Path.Combine(root.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(source is not null && File.Exists(source), "The preserved original ending field transition standalone is required.");
        GameState original = GameSession.Load(source!);
        Assert.Equal(979, original.FieldId);
        Assert.Null(original.EventState);
        Assert.NotNull(original.DepartingFieldState);
        Assert.Equal(974, original.DepartingFieldState.Value.GetProperty("FieldId").GetInt32());
        Assert.Equal(new[] { 0, 255, 255 }, original.DepartingFieldState.Value.GetProperty("Scene").GetProperty("Party").EnumerateArray().Select(value => value.GetInt32()).ToArray());
        Assert.Equal(0x800u, original.WorldControls!.EventFlags104 & 0x800u);
        NativeCharacterVitals before = original.CharacterVitals!.DeepCopy();
        GameSession session = new(original);
        string gameDirectory = TestResources.GameDirectory;
        using GameWorld world = new(session, gameDirectory);
        world.EnsureField();
        Assert.Equal(979, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(before.JunctionPartyBackup, session.CharacterVitals!.JunctionPartyBackup);
        Assert.Equal(before.CombatParty, session.CharacterVitals.CombatParty);
        Assert.Equal(new[] { 0, 255, 255 }, world.Events.Scene.Party);

        session.SetEventState(world.Events.Capture());
        GameState independent = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession resumedSession = new(independent);
        using GameWorld resumed = new(resumedSession, gameDirectory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(before.JunctionPartyBackup, session.CharacterVitals.JunctionPartyBackup);
        Assert.Equal(session.CharacterVitals.JunctionPartyBackup, resumedSession.CharacterVitals!.JunctionPartyBackup);
    }
}
