using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeModelDoorContinuationTests
{
    [Fact]
    public void AuthenticMidDoorRenderingIsPureAndItsExecutedSamplesRestoreAcrossTheNextCallback()
    {
        string directory = GameDirectory();
        string source = Evidence("App.Tests.NativeModelDoorContinuationTests.1");
        GameState recreated = GameSession.Load(source);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(919, world.Events.FieldId);
        Assert.Equal(244, world.Events.Frame);
        NativeModelPartResourceState resource = world.Events.Scene.ModelParts!.Resources[13];
        for (int callback = 0; (resource.Parts[1][7] & 1) == 0; callback++)
        {
            Assert.True(callback < 30, "Actual door animation did not start within its bounded ordinary callback budget.");
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
            resource = world.Events.Scene.ModelParts!.Resources[13];
        }
        short counter = resource.Parts[1][2];
        NativeModelPartDraw sample = world.Events.Scene.ModelParts!.Draws[13];
        Assert.Equal(world.Events.Frame, sample.Frame);
        Assert.NotEmpty(sample.SkinTranslations);
        JsonElement before = world.Events.Capture();
        byte[] pixels = world.Render(TimeSpan.Zero).ToArray();
        for (int render = 0; render < 5; render++)
        {
            Assert.Equal(pixels, world.Render(TimeSpan.Zero));
        }
        world.SetRenderSize(640, 448);
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
        world.SetRenderSize(320, 240);
        Assert.Equal(pixels, world.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal(counter, world.Events.Scene.ModelParts.Resources[13].Parts[1][2]);

        WithRestored(world, session, directory, restored =>
        {
            Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
            PlayerAction next = new(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), restored.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.NotEqual(counter, world.Events.Scene.ModelParts!.Resources[13].Parts[1][2]);
            Assert.Equal(world.Events.Frame, world.Events.Scene.ModelParts.Draws[13].Frame);
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Assert.Null(world.Events.LastFault);
        });
    }

    [Fact]
    public void AuthenticPlayableDoorEndpointColdRecreatesAndContinuesAfterNormalSaveLoad()
    {
        string directory = GameDirectory();
        string source = Evidence("App.Tests.NativeModelDoorContinuationTests.2");
        GameState recreated = GameSession.Load(source);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(919, world.Events.FieldId);
        Assert.Equal(820, world.Events.Frame);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Equal(4, world.Events.Scene.ModelParts!.Resources[13].Parts[1][7]);
        WithRestored(world, session, directory, restored =>
        {
            PlayerAction wait = new(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            ActorState leader = NativeFieldPartyBinding.ResolveActor(world.Events.Scene, world.Events.Actors, 0)!;
            Vector3 before = new(leader.X, leader.Y, leader.Z);
            PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1);
            PlayerActionResult result = world.Apply(move);
            Assert.Equal(result, restored.Apply(move));
            Assert.True(result.Accepted, result.Detail);
            Assert.NotEqual(before, new Vector3(leader.X, leader.Y, leader.Z));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Assert.Null(world.Events.LastFault);
        });
    }

    private static void WithRestored(GameWorld world, GameSession session, string directory, Action<GameWorld> check)
    {
        string saved = Path.Combine(Path.GetTempPath(), "ff8-authentic-modelparts-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.SetEventState(world.Events.Capture());
            session.Save(saved);
            using var restored = new GameWorld(new GameSession(GameSession.Load(saved)), directory);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            check(restored);
        }
        finally
        {
            File.Delete(saved);
        }
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original model/field corpus is required.");
        return directory;
    }

    private static string Evidence(string name)
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return TestResources.FixturePath(name);
    }
}
