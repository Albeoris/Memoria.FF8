using System.Numerics;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldParticleProjectionTests
{
    [Fact]
    public void CameraEulerOrderMatchesNativeMatrixGolden()
    {
        Assert.Equal(new short[] { 1768, -631, 3641, 0, 4036, 700, -3695, -302, 1742 }, NativeWorldParticleMath.Rotation(-112, 733, 0, camera: true));
    }

    [Fact]
    public void RotatedPlaneMatchesNativeProjectionGolden()
    {
        short[] camera = NativeWorldParticleMath.Rotation(-112, 733, 0, camera: true);
        var projected = NativeWorldParticleMath.ProjectQuad(camera, new Vector3(100, -200, 3000), new Vector3(500, -300, 1000), NativeWorldParticleMath.Rotation(73, 1555, -34), 100, 0, 1024);
        Assert.Equal(new[] { (5015, -15), (4407, -46), (4982, 62), (4412, 31) }, projected.Select(point => (point.X, point.Y)));
        Assert.Equal(372, NativeWorldParticleMath.OrderingDepth(projected[0].Depth, projected[1].Depth, projected[2].Depth, projected[3].Depth, 0));
    }
    [Theory]
    [InlineData(0, 398)]
    [InlineData(3, 400)]
    [InlineData(4, 400)]
    public void NativeAverageDepthAndGroundBiasSelectSharedProxy(byte mode, int expected)
    {
        Assert.Equal(expected, NativeWorldParticleMath.OrderingDepth(3000, 3100, 3300, 3400, mode));
        Assert.Equal(-1, NativeWorldParticleMath.OrderingDepth(0, 0, 0, 0, 0));
        Assert.Equal(-1, NativeWorldParticleMath.OrderingDepth(40000, 40000, 40000, 40000, mode));
    }

    [Fact]
    public void ProjectionPreservesUnsignedDepthClampAndFocalHalfDenominator()
    {
        Assert.True(NativeWorldParticleMath.OrderingReciprocal(0) > NativeWorldParticleMath.OrderingReciprocal(1));
        Assert.True(float.IsFinite(NativeWorldParticleMath.OrderingReciprocal(0)));
        Assert.Equal((1296, 880, 0), NativeWorldParticleMath.Project(new Vector3(1, -1, -40), 1024));
        Assert.Equal((8184, -8192, 100), NativeWorldParticleMath.Project(new Vector3(32767, -32768, 100), 1024));
    }
}
