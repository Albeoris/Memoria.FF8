using System.Security.Cryptography;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldWalkingIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public void AuthenticWorldFootWithAuxiliaryObjectsWalksThroughItsNativeEntrance()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayWorldWalkingIntegrationTests.1");
        byte[] original = Hash(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        GameState before = session.State;
        WorldMapScene scene = world.WorldScene!;
        Assert.Equal(SceneKind.WorldMap, before.Scene);
        Assert.Equal<ushort>(290, before.WorldEntryMemory!.StoryWord);
        Assert.NotNull(before.World!.NativeFootRuntime!.NormalObjects);
        try
        {
            IReadOnlyList<WorldAutoPlayTarget> targets = scene.DiscoverAutoPlayEntrances();
            WorldAutoPlayTarget entrance = Assert.Single(targets, target => target.DestinationField == 899);
            Assert.Same(before, session.State);
            Assert.NotEmpty(scene.GetAutoPlayEntrancePath(entrance));
            Assert.Same(before, session.State);

            int movement = 0;
            WorldAutoPlayTravelResult travel = world.ExecuteAutoPlayWorldTarget(entrance, () =>
            {
                Assert.NotNull(session.State.World!.NativeFootRuntime!.NormalObjects);
                movement++;
            });

            output.WriteLine($"World movement callbacks={travel.Action.FramesAdvanced}; result={travel.Action.Trigger}; progress={movement}; detail={travel.Action.Detail}");
            Assert.True(travel.MatchedTarget, travel.Action.Detail);
            Assert.Equal(PlayerTriggerKind.FieldTransition, travel.Action.Trigger.Kind);
            Assert.Equal(899, session.State.FieldId);
            Assert.True(movement > 0);
            Assert.InRange(travel.Action.FramesAdvanced, 1, 256);
            Assert.NotNull(scene.LastDeparture!.NativeFootRuntime!.NormalObjects);
            Assert.Equal<ushort>(290, session.State.WorldEntryMemory!.StoryWord);
        }
        finally
        {
            Assert.Equal(original, Hash(source));
        }
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
