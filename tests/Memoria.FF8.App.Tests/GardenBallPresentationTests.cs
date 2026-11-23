using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GardenBallPresentationTests
{
    [Fact]
    public void UniformDepartureRendersTheBallThroughTheNativeHeldPose()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.GardenBallPresentationTests.1");
        byte[] original = File.ReadAllBytes(source);
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Load(source, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.Equal(241, session.State.FieldId);

        AutoPlayController controller = new(session, () => world, session.Restore, TestResources.ArtifactDirectory);
        controller.StartFromCurrentState();
        List<int> fields = [session.State.FieldId];
        for (int step = 0; step < 160 && session.State.FieldId != 221 && controller.Error is null; step++)
        {
            controller.Advance(1);
            fields.Add(session.State.FieldId);
        }
        Assert.Null(controller.Error);
        Assert.Contains(243, fields);
        Assert.Equal(221, session.State.FieldId);

        bool sawSelection = false;
        bool sawCompletion = false;
        for (int callback = 0; callback < 30; callback++)
        {
            world.EnsureField();
            if (world.CanPresentCurrentField && world.Events.Frame >= 3)
            {
                ActorState lead = Assert.Single(world.Events.Actors, actor => actor.ObjectId == 0);
                if (world.Events.Frame == 3)
                {
                    Assert.Equal(1, world.Field!.GetAnimationFrameCount(lead.RenderModelId, 1));
                    Assert.True(lead.NativeAnimationHeader is { AnimationId: 1, Frame: 16, First: 16, Terminal: 16, Flags: 0x8000 });
                    sawSelection = true;
                }
                sawCompletion |= lead.NativeAnimationHeader is { AnimationId: 1, Frame: 0, First: 16, Terminal: 0, Flags: 0x1800 };
                var state = world.Events.Capture();
                foreach (float fraction in new[] { 0, 0.5f, 1 })
                {
                    Assert.NotEmpty(world.Render(TimeSpan.Zero, fieldInterpolation: fraction, smoothFieldAnimation: true));
                }
                Assert.True(JsonElement.DeepEquals(state, world.Events.Capture()));
            }
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(sawSelection);
        Assert.True(sawCompletion);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
