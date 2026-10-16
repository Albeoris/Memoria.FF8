using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GardenEarlyDepartureTests
{
    [Fact]
    public void ActualStory17DoorDepartureSelectsFootAndContinuesAfterStandaloneRestore()
    {
        string directory = GameDirectory();
        GameSession session = new(GameSession.Load(Source()));
        Assert.Equal(159, session.State.FieldId);
        Assert.Equal(1012, session.State.EventState!.Value.GetProperty("Frame").GetInt32());
        Assert.Equal((ushort)17, session.State.WorldEntryMemory!.StoryWord);
        using GameWorld world = new(session, directory);
        Assert.True(world.EnsureField());
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        for (int callback = 0; callback < 16; callback++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Move, Vertical: 1)).Accepted);
        }
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(1029, world.Events.Frame);
        Assert.False(world.Events.IsUserControlEnabled);
        // The actual door fiber13/script104 is now immediately before its world-entry operation2138.
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(new WorldTransition(0, 0, -1), session.State.WorldArrival);
        AssertEarlyFoot(session.State.World!);
        Assert.Equal((ushort)17, session.State.WorldEntryMemory!.StoryWord);

        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-Garden-early-departure-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, directory);
            Assert.True(restored.EnsureField());
            AssertEarlyFoot(restoredSession.State.World!);
            foreach (PlayerAction input in new PlayerAction[] { new(PlayerActionKind.Wait), new(PlayerActionKind.Move, Horizontal: 1) })
            {
                PlayerActionResult result = world.Apply(input);
                Assert.True(result.Accepted);
                Assert.Equal(result, restored.Apply(input));
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
                AssertEarlyFoot(restoredSession.State.World!);
            }
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public void UnselectedSavedVehicleCoordinatesCannotChangeEarlyFootAllocation(bool moveGarden, bool moveRagnarok, bool missingUnusedPoses)
    {
        string directory = GameDirectory();
        GameState original = GameSession.Load(Source());
        NativeSavedWorldState saved = original.SavedWorld!;
        // Declared selector component: vary only unused saved coordinates; this does not claim either vehicle was acquired.
        GameState fixture = original with
        {
            SavedWorld = saved with
            {
                GardenPose3 = missingUnusedPoses ? null : moveGarden ? new(1024, -2048, 128, 256) : saved.GardenPose3,
                RagnarokPose2 = missingUnusedPoses ? null : moveRagnarok ? new(-4096, 8192, -32, 512) : saved.RagnarokPose2
            }
        };
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(fixture, directory, new WorldTransition(0, 0, -1));
        AssertEarlyFoot(prepared.InitialState);
        Assert.Equal(fixture.SavedWorld.GardenPose3, prepared.InitialSavedWorld!.GardenPose3);
        Assert.Equal(fixture.SavedWorld.RagnarokPose2, prepared.InitialSavedWorld.RagnarokPose2);
        Assert.Equal((ushort)17, WorldEntryVariables.Read(fixture).StoryWord);
    }

    private static void AssertEarlyFoot(WorldState state)
    {
        Assert.NotNull(state.NativeFootRuntime);
        Assert.Equal((byte)0, state.NativeFootRuntime.EntityType);
        Assert.Null(state.NativeGardenRuntime);
        Assert.Null(state.NativeRagnarokRuntime);
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field and world resources are required.");
        return directory;
    }

    private static string Source()
    {
        return TestResources.FixturePath("App.Tests.GardenEarlyDepartureTests.1");
    }
}

