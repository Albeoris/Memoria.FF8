using System.Numerics;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldCameraTests
{
    [Fact]
    public void NativeFootOpticalAxisRetainsPackedOffsetAndPitchRatherThanLookingAtOrbitTarget()
    {
        WorldCamera camera = WorldCamera.Foot(0, 2048);
        WorldCameraFrame frame = camera.FootFrame(Vector3.Zero);
        Assert.InRange(Vector3.Distance(frame.Eye, new Vector3(0, 1817.4345f, 7004.1721f)), 0, 0.001f);
        Assert.InRange(frame.Forward.Y, -0.171f, -0.170f);
        Assert.InRange(frame.Forward.Z, -0.986f, -0.985f);
        Assert.InRange(Vector3.Dot(frame.Forward, frame.Up), -0.00001f, 0.00001f);
        Assert.InRange(camera.FieldOfView * 180 / MathF.PI, 12.483f, 12.485f);
        Vector3 targetDirection = Vector3.Normalize(new Vector3(0, 256, 0) - frame.Eye);
        Assert.True(Vector3.Distance(frame.Forward, targetDirection) > 0.04f);
        WorldCameraFrame quarter = WorldCamera.Foot(1024, 2048).FootFrame(Vector3.Zero);
        Assert.InRange(Vector3.Distance(quarter.Eye, new Vector3(-7004.1721f, 1817.4345f, 0)), 0, 0.002f);
    }
}
