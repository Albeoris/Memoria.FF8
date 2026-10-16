using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeInitialModelPresentationTests
{
    [Fact]
    public void AuthenticInfirmaryAdmissionWaitsForItsFirstExecutedSkinCallback()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original opening movie and field corpus is required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (world.Field!.Id == 180 && world.Events.Scene.NativeFieldInitialization?.Stage == NativeFieldInitializationStage.Completed)
            {
                Assert.Equal(1, world.Events.Frame);
                NativeFieldModelPartsState parts = Assert.IsType<NativeFieldModelPartsState>(world.Events.Scene.ModelParts);
                Assert.Contains(parts.Instances, instance => world.Events.Actors[instance.ObjectId] is { Active: true, Visible: true });
                Assert.Empty(parts.Draws);
                JsonElement initialized = world.Events.Capture();
                Assert.False(world.CanPresentCurrentField);
                Assert.True(JsonElement.DeepEquals(initialized, world.Events.Capture()));

                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
                Assert.Equal(2, world.Events.Frame);
                Assert.True(world.CanPresentCurrentField);
                Assert.All(world.Events.Scene.ModelParts!.Draws.Values, draw => Assert.Equal(world.Events.Frame, draw.Frame));
                JsonElement drawn = world.Events.Capture();
                byte[] pixels = world.Render(TimeSpan.Zero);
                Assert.NotEmpty(pixels);
                Assert.Equal(pixels, world.Render(TimeSpan.Zero));
                Assert.True(JsonElement.DeepEquals(drawn, world.Events.Capture()));
                Assert.Null(world.Events.LastFault);

                //A missing sample after a real callback remains an unsupported contract,
                //rather than becoming an indefinite presentation delay.
                NativeFieldModelInstance visible = parts.Instances.First(instance => world.Events.Actors[instance.ObjectId] is { Active: true, Visible: true });
                NativeModelPartDraw sample = world.Events.Scene.ModelParts.Draws[visible.ObjectId];
                world.Events.Scene.ModelParts.Draws.Remove(visible.ObjectId);
                Assert.True(world.CanPresentCurrentField);
                Assert.Throws<NotSupportedException>(() => world.Render(TimeSpan.Zero));
                world.Events.Scene.ModelParts.Draws.Add(visible.ObjectId, sample);
                return;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction action = session.IsMoviePlaying ? new(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int choice ? new(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Fail("The authentic opening did not reach completed infirmary initialization within the ordinary input budget.");
    }
}
