using System.Numerics;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7ChocoboInterpolationTests
{
    [Fact]
    public void CameraEndpointsStayExactAndHalfStepKeepsRigidRotation()
    {
        Matrix4x4 previous = Matrix4x4.CreateRotationY(0.1f);
        previous.Translation = new(100, 20, -30);
        Matrix4x4 current = Matrix4x4.CreateRotationY(3);
        current.Translation = new(200, 40, 10);
        Assert.Equal(previous, Ff7ChocoboInterpolation.Transform(previous, current, 0));
        Assert.Equal(current, Ff7ChocoboInterpolation.Transform(previous, current, 1));
        Matrix4x4 middle = Ff7ChocoboInterpolation.Transform(previous, current, 0.5f);
        Assert.Equal(new Vector3(150, 30, -10), middle.Translation);
        Assert.InRange(middle.GetDeterminant(), 0.999f, 1.001f);
    }

    [Fact]
    public void AnimationWrapInterpolatesAcrossTheLastAndFirstNativeFrames()
    {
        Assert.Equal(39, Ff7ChocoboInterpolation.Frame(39, 0, 40, 0, true));
        Assert.Equal(39.5f, Ff7ChocoboInterpolation.Frame(39, 0, 40, 0.5f, true));
        Assert.Equal(0, Ff7ChocoboInterpolation.Frame(39, 0, 40, 1, true));
        Assert.Equal(10, Ff7ChocoboInterpolation.Frame(12, 8, 40, 0.5f, false));
    }
}
