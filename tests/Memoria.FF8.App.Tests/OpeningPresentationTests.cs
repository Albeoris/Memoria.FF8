using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OpeningPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshOpeningRendersEveryFieldUpdateThroughPlayableCorridor(bool smoothAnimation)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory);
        HashSet<int> renderedFields = [];
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (session.State.Scene == SceneKind.Field)
            {
                if (RenderOpeningFrame(world, smoothAnimation))
                {
                    renderedFields.Add(world.Field!.Id);
                }
                if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
                {
                    Assert.Contains(180, renderedFields);
                    Assert.Contains(181, renderedFields);
                    Assert.Null(world.Events.LastFault);
                    string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OPENING_OUTPUT") ?? TestResources.ArtifactDirectory;
                    Directory.CreateDirectory(output);
                    string snapshot = Path.Combine(output, $"opening-{(smoothAnimation ? 60 : 30)}fps.json");
                    session.SetEventState(world.Events.Capture());
                    session.Save(snapshot, compact: true);
                    using GameWorld restored = new(new GameSession(GameSession.Load(snapshot)), directory);
                    restored.EnsureField();
                    Assert.Equal(world.Events.FieldId, restored.Events.FieldId);
                    Assert.Equal(world.Events.Frame, restored.Events.Frame);
                    Assert.NotEmpty(restored.Render(TimeSpan.Zero, smoothFieldAnimation: smoothAnimation));
                    return;
                }
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction action = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue is { CanConfirm: true, IsNamePrompt: true } ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue is { CanConfirm: true, FirstChoice: int choice } ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            PlayerActionResult result = world.Apply(action);
            Assert.True(result.Accepted, $"Opening input {action} at field{world.Events.FieldId}/frame{world.Events.Frame}: {result.Detail}");
            world.CommitFieldPresentation();
        }
        Assert.Fail($"Fresh opening presentation exceeded its bounded action budget: field{world.Events.FieldId}/frame{world.Events.Frame}, trigger {world.Events.NextTrigger}, dialogue {world.Events.CurrentDialogue}, fault {world.Events.LastFault}.");
    }

    private static bool RenderOpeningFrame(GameWorld world, bool smoothAnimation)
    {
        if (FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization) || !world.CanPresentCurrentField)
        {
            return false;
        }
        try
        {
            foreach (ActorState actor in world.Events.Actors)
            {
                var sample = world.CaptureFieldActor(actor);
                if (sample.Visual.Visible && sample.Visual.ModelId >= 0)
                {
                    int count = world.Field!.GetAnimationFrameCount(sample.Visual.ModelId, sample.Visual.AnimationId);
                    Assert.True(sample.FirstFrame >= 0 && sample.TerminalFrame > sample.FirstFrame && sample.TerminalFrame <= count, JsonSerializer.Serialize(new { Field = world.Events.FieldId, world.Events.Frame, actor.ObjectId, sample.Visual.ModelId, sample.Visual.AnimationId, sample.Visual.AnimationFrame, sample.FirstFrame, sample.TerminalFrame, Count = count }));
                }
            }
            foreach (float fraction in smoothAnimation ? new[] { 0, 0.5f, 1 } : new[] { 1f })
            {
                Assert.NotEmpty(world.Render(TimeSpan.Zero, fieldInterpolation: fraction, smoothFieldAnimation: smoothAnimation));
            }
        }
        catch (NotSupportedException exception)
        {
            SceneEventState scene = world.Events.Scene;
            ActorState? leader = scene.FieldPartyNativeActorIds.Count > 0 && scene.FieldPartyNativeActorIds[0] < scene.NativeActorObjectIds.Count ? world.Events.Actors.FirstOrDefault(actor => actor.ObjectId == scene.NativeActorObjectIds[scene.FieldPartyNativeActorIds[0]]) : null;
            FieldFixedPosition? position = scene.NativePlayerPosition;
            if (leader is not null && (position is not FieldFixedPosition raw || leader.X != raw.X / 4096f || leader.Y != raw.Y / 4096f || leader.Z != raw.Z / 4096f))
            {
                position = new FieldFixedPosition((int)MathF.Floor(leader.X) << 12, (int)MathF.Floor(leader.Y) << 12, (int)MathF.Floor(leader.Z) << 12);
            }
            FieldNativeCameraResult? probe = position is FieldFixedPosition fixedPosition && world.Field!.Information is FieldInformation information ? FieldNativeCameraProbe.Resolve(world.Field.Camera, information, fixedPosition.X, fixedPosition.Y, fixedPosition.Z, scene.CameraIndex) : null;
            Assert.Fail(JsonSerializer.Serialize(new { exception.Message, Field = world.Events.FieldId, world.Events.Frame, scene.CameraIndex, Leader = leader is null ? null : new { leader.ObjectId, leader.X, leader.Y, leader.Z }, Position = position, Probe = probe }));
        }
        return true;
    }
}
