using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class CommittedPresentationHistoryTests
{
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
}
