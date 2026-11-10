using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7PartVisibilityPresentationTests
{
    [Fact]
    public void PendingAndAppliedNativeVisibilityPreserveOriginalLightingAndColors()
    {
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, 0, 0);
        Ff7NativePartVisibilityState state = Ff7NativePartVisibility.Request(null, [2, 0, 0, 2, 0], 3);
        var pending = Ff7Scene.CreateModelEffects(null, camera, null, state)!;
        Assert.All(Enumerable.Range(0, 3), part => Assert.True(pending.PartVisible(part)));
        Assert.Null(pending.VertexColor);
        Assert.False(pending.SuppressNativeLighting);
        Ff7NativePartVisibilityState applied = Ff7NativePartVisibility.Advance(state, true, 186);
        var rendered = Ff7Scene.CreateModelEffects(null, camera, null, applied)!;
        Assert.False(rendered.PartVisible(0));
        Assert.True(rendered.PartVisible(1));
        Assert.False(rendered.PartVisible(2));
        Assert.Null(rendered.VertexColor);
        Assert.False(rendered.SuppressNativeLighting);
        Assert.All(state.Visibility, value => Assert.Equal((byte)1, value));
        Assert.Null(Ff7Scene.CreateModelEffects(null, camera, null));
    }
}
