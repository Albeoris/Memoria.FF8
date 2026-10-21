using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class FieldActorCaptureTests
{
    [Theory]
    [InlineData(1, 0, 0, false, 0, 1, 0)]
    [InlineData(1, 0, 0.5f, false, 0, 2, 5)]
    [InlineData(0, 0, 1.5f, false, 0, 3, 15)]
    [InlineData(1, 3, 3.5f, false, 1, 4, 30)]
    [InlineData(1, 2, 2.5f, true, 1, 3, 15)]
    public void SamplingIncludesHeldPoseAndPreservesPlaybackState(int first, int last, float phase, bool loop, int expectedFirst, int expectedTerminal, float expectedX)
    {
        JsonObject definition = JsonSerializer.SerializeToNode(new ActorState(1, 0))!.AsObject();
        definition["AnimationFirst"] = first;
        definition["AnimationLast"] = last;
        definition["AnimationFrame"] = phase;
        definition["AnimationLoop"] = loop;
        definition["AnimationPlaying"] = loop;
        definition["AnimationKeep"] = !loop;
        ActorState actor = definition.Deserialize<ActorState>()!;
        string state = JsonSerializer.Serialize(actor);
        FieldModel model = new("sample", [new(-1, 0)], [new(Vector3.UnitY, 0)], [], [], [new(Enumerable.Range(0, 4).Select(index => new AnimationFrame(new(index * 10, 0, 0), [Vector3.Zero])).ToArray())]);
        ActorVisual visual = new(1, 0, 0, 0, 0, 0, 0, phase, true);
        FieldActorSample captured = GameWorld.CaptureFieldActor(actor, visual, model.Animations[0].Frames.Count);
        Assert.Equal(expectedFirst, captured.FirstFrame);
        Assert.Equal(expectedTerminal, captured.TerminalFrame);
        FieldPresentationFrame frame = new(new object(), 0, 0, [captured], new BackgroundScrollFrame(new Dictionary<int, Vector2>(), Vector2.Zero));
        FieldPresentation presentation = new();
        foreach (float fraction in new[] { 0, 0.5f, 1 })
        {
            ActorVisual sample = Assert.Single(presentation.Sample(frame, fraction, true).Actors);
            FieldAnimationBlend blend = sample.AnimationBlend!.Value;
            Vector3 pose = Assert.Single(model.PoseInterpolated(sample.AnimationId, blend.PreviousFrame, blend.CurrentFrame, blend.Fraction, firstFrame: blend.FirstFrame, terminalFrame: blend.TerminalFrame, loop: blend.Loop));
            Assert.Equal(expectedX, pose.X, 4);
            Assert.Equal(1, pose.Y, 4);
            Assert.Equal(0, pose.Z, 4);
        }
        Assert.Equal(state, JsonSerializer.Serialize(actor));
    }
}
