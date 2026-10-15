using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarFieldRenderTests
{
    [Fact]
    public void ActualCarPrefixUsesSharedDirectCenterAndRestoresDormantShakeWithoutChangingPartyAliases()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Authentic route snapshot and installed game are required.");
        GameState current = SnapshotFixtures.Require("MEMORIA_FF8_CAR_DEPARTURE_SNAPSHOT", 262);
        Assert.Equal(262, current.FieldId);
        GameSession session = new(current);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(new[] { 0, 6, 1 }, world.Events.Scene.Party);
        Assert.Equal(new[] { 0, 255, 255 }, world.Events.Scene.FieldPartyNativeActorIds);
        Assert.True(DirectBackgroundScrollCertificate.CanSet(world.Field!, world.Events.Scene, 0, -3, 4));
        Assert.False(DirectBackgroundScrollCertificate.CanSet(world.Field!, world.Events.Scene, 1, 0, 0));
        Assert.NotNull(DirectBackgroundScrollCertificate.Prepare(world.Field!, world.Events.Scene));
        Assert.Equal(0, world.Events.FieldShake!.X.Output);
        Assert.Equal(0, world.Events.FieldShake.Y.Output);
        byte[] pixels = world.Render(TimeSpan.Zero);
        JsonElement vm = world.Events.Capture();
        GameState saved = session.State with { EventState = vm };
        using GameWorld restored = new(new GameSession(saved), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(vm, restored.Events.Capture()));
        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
        Assert.Equal(world.Events.Variables[194], restored.Events.Scene.PartyBindingMemory!.First);
        Assert.Equal(world.Events.Variables[195], restored.Events.Scene.PartyBindingMemory.Second);
        Assert.Equal(world.Events.Variables[196], restored.Events.Scene.PartyBindingMemory.Third);
        world.Apply(new PlayerAction(PlayerActionKind.Wait));
        restored.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }
}
