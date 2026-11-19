using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class CommittedPresentationHistoryTests
{
    [Fact]
    public void Ff7WorldAttachedRidersStayOutOfTheCommittedVisualsForEveryVehicle()
    {
        WorldEntitySnapshot rider = new(0, 0, 0, 0, false, 0, []) { Detached = true };
        WorldSnapshot vm = new(1, "presentation", 0, 0, false, [], [], new(-1, 0, 0, 0, false, 0, []), [rider, new(5, 0, 0, 0, false, 0, [])]);
        Ff7WorldProcess state = new(0, 0, 0, vm, [new(0), new(5) { AttachedModel = 0 }], 5, true, 0, 0, null, [], -1, new(), 0, 1);
        Assert.Equal(5, Assert.Single(Ff7Scene.WorldRenderFrame(state).Models).Model);
        Ff7WorldProcess dismounted = state with { Vm = vm with { Models = [rider with { Detached = false }, vm.Models[1]] }, ControlledModel = 0, Entities = [new(0), new(5)] };
        Assert.Equal(new[] { 0, 5 }, Ff7Scene.WorldRenderFrame(dismounted).Models.Select(model => model.Model));
    }

    [Fact]
    public void CallbackEndpointsAreRetainedAndPreparationFailureIsDeferred()
    {
        var history = new CommittedPresentationHistory<(object Owner, int Tick), string>((first, second) => first == second, (first, second) => first.Owner == second.Owner && second.Tick == first.Tick + 1);
        object owner = new();
        string globals = "first";
        history.Commit((owner, 1), () => globals);
        Assert.Equal((null, "first"), history.Sample((owner, 1), 1, () => globals));
        globals = "second";
        history.Commit((owner, 2), () => globals);
        globals = "draw";
        Assert.Equal(("first", "second"), history.Sample((owner, 2), 0.5f, () => globals));
        Assert.Equal((null, "second"), history.Sample((owner, 2), 1, () => globals));
        InvalidDataException failure = new("Presentation failure.");
        history.Commit((owner, 3), () => throw failure);
        Assert.Same(failure, Assert.Throws<InvalidDataException>(() => history.Sample((owner, 3), 1, () => globals)));
        history.Reset();
        Assert.Equal((null, "draw"), history.Sample((owner, 3), 0.5f, () => globals));
        history.Commit((new object(), 4), () => "replacement");
        Assert.Null(history.Previous);
    }

    [Fact]
    public void FrameValidationAndSkippedCallbacksBreakHistoryUntilAnotherContinuousCommit()
    {
        var history = new CommittedPresentationHistory<int, string>((first, second) => first == second, (first, second) => second == first + 1, (_, second) => second != "cut");
        Assert.Equal((null, "start"), history.Sample(0, 0.5f, () => "start"));
        history.Commit(1, () => "next");
        Assert.Equal(("start", "next"), history.Sample(1, 0.5f, () => "unused"));
        history.Commit(2, () => "cut");
        Assert.Equal((null, "cut"), history.Sample(2, 0.5f, () => "unused"));
        history.Commit(4, () => "skipped");
        Assert.Equal((null, "skipped"), history.Sample(4, 0.5f, () => "unused"));
        history.Commit(5, () => "before edit");
        Assert.Equal((null, "edited"), history.Sample(5, 0.5f, () => "edited", frame => frame == "edited"));
        history.Commit(6, () => "after edit");
        Assert.Equal(("edited", "after edit"), history.Sample(6, 0.5f, () => "unused"));
        history.Reset();
        history.Commit(7, () => "after reset");
        Assert.Equal((null, "after reset"), history.Sample(7, 0.5f, () => "unused"));
    }

    [Theory]
    [InlineData(30, 30d, 0.5d, 1f)]
    [InlineData(60, 30d, 0.5d, 0.5f)]
    [InlineData(60, 30.5d, 0.5d, 0.5f)]
    [InlineData(120, 20d, -0.5d, 0f)]
    [InlineData(120, 20d, 1.5d, 1f)]
    public void PresentationFractionUsesTheSceneCallbackRate(int presentationFps, double callbackRate, double remainderInCallbacks, float expected)
    {
        float fraction = CommittedPresentationTime.ShouldInterpolate(presentationFps, callbackRate) ? CommittedPresentationTime.Fraction(remainderInCallbacks / callbackRate, callbackRate) : 1f;
        Assert.Equal(expected, fraction);
    }

    [Theory]
    [InlineData(30, 4)]
    [InlineData(60, 2)]
    public void Ff7FieldDiscontinuitiesPreserveTheNativeEndpoint(int fps, int expectedX)
    {
        Ff7Actor previous = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, Visible = true, AnimationFrame16 = 32 };
        Ff7Actor current = previous with { X4096 = 4 * 4096, AnimationFrame16 = 48 };
        Ff7FieldProcess first = new() { Actors = [previous] };
        Ff7FieldProcess second = first with { Actors = [current], CameraX = 4 };
        float fraction = fps == 30 ? 1 : 0.5f;
        Assert.Equal(expectedX * 4096, Ff7ActorPresentation.Interpolate(previous, current, fraction, 8).X4096);
        Assert.Equal(expectedX, Ff7FieldPresentation.InterpolateCameraOffset(first, second, fraction).X);
        Ff7Actor placed = current with { PlacementEpoch = 1 };
        Assert.Same(placed, Ff7ActorPresentation.Interpolate(previous, placed, fraction, 8));
        Assert.Equal(4, Ff7FieldPresentation.InterpolateCameraOffset(first, second with { Actors = [placed] }, fraction).X);
        Assert.Equal(4, Ff7FieldPresentation.InterpolateCameraOffset(first, second with { CameraEpoch = 1 }, fraction).X);
        Ff7Actor restarted = current with { AnimationFrame16 = 16, AnimationEpoch = 1 };
        Assert.Equal(16, Ff7ActorPresentation.Interpolate(previous, restarted, fraction, 8).AnimationFrame16);
        Ff7Actor changed = current with { AppearanceEpoch = 1 };
        Assert.Same(changed, Ff7ActorPresentation.Interpolate(previous, changed, fraction, 8));
        Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(placed));
    }

    [Theory]
    [InlineData(30, 2)]
    [InlineData(60, 0)]
    public void Ff7WorldFollowsBothNativeSeamsAtIntermediateFractions(int fps, int expected)
    {
        Ff7WorldVisual first = new(0, new(0x48000 - 2, 0, 0x38000 - 2), 4094, 1, 0, 2);
        Ff7WorldVisual second = first with { Position = new(2, 8, 2), Direction = 2 };
        Ff7WorldRenderFrame previous = new(0, true, 1, 2, 0, 0, 0, 0, new(4094, 0, 100, 0, 0, 400), [first], []);
        Ff7WorldRenderFrame current = previous with { Models = [second], Camera = previous.Camera with { Heading = 2 } };
        Assert.True(Ff7WorldRenderInterpolation.Continuous(previous, current));
        Ff7WorldRenderFrame sampled = Ff7WorldRenderInterpolation.Interpolate(previous, current, fps == 30 ? 1 : 0.5f);
        Assert.Equal(new Vector3(expected, fps == 30 ? 8 : 4, expected), Assert.Single(sampled.Models).Position);
        Assert.Equal(expected, sampled.Camera.Heading);
        Assert.Equal(0x36000, Ff7WorldRuntime.InterpolateWorldCoordinate(0, 0x24000, 0x48000, 0.5f));
        Assert.False(Ff7WorldRenderInterpolation.Continuous(previous, current with { Models = [second with { PresentationEpoch = 1 }] }));
        Assert.False(Ff7WorldRenderInterpolation.Continuous(previous, current with { Models = [second with { Flags = 10 }] }));
        Assert.Equal(new Vector3(2, 8, 2), Assert.Single(current.Models).Position);
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(7, 0)]
    [InlineData(0, 3)]
    public void Ff7WorldBlendsCommittedPoseEndpointsIncludingWrapsAndSkippedFrames(int firstFrame, int secondFrame)
    {
        Ff7WorldVisual first = new(0, Vector3.Zero, 0, 1, firstFrame, 2);
        Ff7WorldVisual second = first with { Position = new(20, 4, 10), Frame = secondFrame };
        Ff7WorldRenderFrame previous = new(0, true, 1, 0, 0, 0, 0, 0, new(0, 0, 100, 0, 4094, 400), [first], []);
        Ff7WorldRenderFrame current = previous with { Models = [second], Camera = previous.Camera with { Roll = 2 } };
        Ff7WorldRenderFrame sampled = Ff7WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        Assert.Equal(new Vector3(10, 2, 5), sampled.Models[0].Position);
        Assert.Equal(new Ff7FieldPoseBlend(firstFrame, secondFrame, 0.5f), sampled.Models[0].AnimationBlend);
        Assert.Equal(0, sampled.Camera.Roll);
        Assert.Same(current, Ff7WorldRenderInterpolation.Interpolate(previous, current, 1));
        Assert.Null(current.Models[0].AnimationBlend);
        Assert.Equal(firstFrame, Ff7WorldRenderInterpolation.Interpolate(previous, current, 0).Models[0].AnimationBlend!.Value.PreviousFrame);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ff7WorldRenderInterpolation.Interpolate(previous, current, float.NaN));
    }

    [Fact]
    public void Ff7WorldClipChangesAndRestartsKeepMovementSmoothWithoutBlendingUnrelatedPoses()
    {
        Ff7WorldVisual first = new(0, Vector3.Zero, 0, 1, 7, 2);
        Ff7WorldRenderFrame previous = new(0, true, 1, 0, 0, 0, 0, 0, new(0, 0, 100, 0, 0, 400), [first], []);
        foreach (Ff7WorldVisual second in new[] { first with { Animation = 2 }, first with { PresentationAnimationEpoch = 1 } })
        {
            Ff7WorldRenderFrame current = previous with { Models = [second with { Position = new(20, 0, 0), Frame = 0 }] };
            Assert.True(Ff7WorldRenderInterpolation.Continuous(previous, current));
            Ff7WorldVisual sampled = Ff7WorldRenderInterpolation.Interpolate(previous, current, 0.5f).Models[0];
            Assert.Equal(10, sampled.Position.X);
            Assert.Null(sampled.AnimationBlend);
            Assert.Equal(0, sampled.Frame);
        }
    }

    [Fact]
    public void Ff7WorldEffectsKeepSmoothMotionAcrossSpriteChangesAndShareTheBridgeAndCloudInterval()
    {
        Ff7WorldVisual leader = new(0, Vector3.Zero, 0, 0, 0, 2);
        Ff7WorldEffectVisual particle = new(1, Vector3.Zero, 0, 4094, false, 16, 16, Vector4.Zero);
        Ff7WorldRenderFrame previous = new(0, true, 1, 0, 0, 0, 0, 255, new(0, 0, 100, 0, 0, 400), [leader], []) { Effects = [particle], BridgeDeformation = new(0, -8), MarshSegments = [new(0, Vector3.Zero, 4094)], SnowLayers = [new(1, 16, 255, 1)] };
        Ff7WorldRenderFrame current = previous with { CloudPhase = 1, Effects = [particle with { Position = new(20, 0, 0), Sprite = 1, Angle = 2, Width = 32, Height = 32, Color = Vector4.One }], BridgeDeformation = new(0, 8), MarshSegments = [new(0, new(20, 0, 0), 2)], SnowLayers = [new(1, 32, 1, 255)] };
        Ff7WorldRenderFrame sampled = Ff7WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        Assert.Equal(new Vector3(10, 0, 0), sampled.Effects[0].Position);
        Assert.Equal(1, sampled.Effects[0].Sprite);
        Assert.Equal(32, sampled.Effects[0].Width);
        Assert.Equal(new Vector4(0.5f), sampled.Effects[0].Color);
        Assert.Equal(0, sampled.Effects[0].Angle);
        Assert.Equal(0, sampled.CloudPhase);
        Assert.Equal(0, sampled.BridgeDeformation!.Tilt);
        Assert.Equal(new Vector3(10, 0, 0), sampled.MarshSegments[0].Position);
        Assert.Equal(0, sampled.MarshSegments[0].Direction % 4096);
        Assert.Equal(24, sampled.SnowLayers[0].Brightness);
        Assert.Equal(0, sampled.SnowLayers[0].U % 256);
        Assert.Equal(0, sampled.SnowLayers[0].V % 256);
        Ff7WorldRenderFrame growing = current with { Effects = [current.Effects[0] with { Sprite = 0 }] };
        Assert.Equal(24, Ff7WorldRenderInterpolation.Interpolate(previous, growing, 0.5f).Effects[0].Width);
        Assert.Equal(Vector3.Zero, previous.Effects[0].Position);
        Assert.Equal(Vector4.One, current.Effects[0].Color);
    }
}
