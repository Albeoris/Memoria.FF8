using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldPresentationTests
{
    [Fact]
    public void SharedNearClipPreservesPerspectiveInputsAndOptionalColors()
    {
        CameraSpaceVertex a = new(new(-3, 0, 0), new(0, 1), Vector4.One);
        CameraSpaceVertex b = new(new(3, 0, 2), new(1, 0), Vector4.Zero);
        CameraSpaceVertex c = new(new(0, 3, 2), new(1, 1));
        Span<CameraSpaceVertex> clipped = stackalloc CameraSpaceVertex[4];
        int count = CameraSpaceTriangleSubmission.ClipNear([a, b, c], clipped);
        Assert.Equal(4, count);
        Assert.Equal(1, clipped[0].Position.Z);
        Assert.Null(clipped[0].Color);
        Assert.Equal(new Vector3(0, 0, 1), clipped[1].Position);
        Assert.Equal(new Vector2(0.5f, 0.5f), clipped[1].Uv);
        Assert.Equal(new Vector4(0.5f), clipped[1].Color);
        CameraSpaceScreenVertex projected = CameraSpaceTriangleSubmission.Project(b, 640, 480, 400);
        Assert.Equal(new Vector2(920, 240), projected.Position);
        Assert.Equal(0.5f, projected.ReciprocalZ);
        Assert.Equal(b.Uv * 0.5f, projected.UvOverZ);
        Assert.Equal(0, CameraSpaceTriangleSubmission.ClipNear([a, a, a], clipped));
    }

    [Fact]
    public void NativeCurvatureStartsBeyondThresholdAndRetainsLateralSymmetry()
    {
        Ff7WorldView view = new(Matrix4x4.Identity, Matrix4x4.Identity, 400, 320, 240, 120);
        Vector3 near = new(100, 200, 20000);
        Assert.Equal(near, Ff7WorldTerrainAdapter.Curve(near, view, 5000));
        Vector3 far = new(100, 200, 24000);
        Vector3 left = Ff7WorldTerrainAdapter.Curve(far with { X = -100 }, view, 5000);
        Vector3 right = Ff7WorldTerrainAdapter.Curve(far, view, 5000);
        Assert.Equal(left.Y, right.Y);
        Assert.True(right.Y < far.Y);
        Assert.Equal(far.Z, right.Z);
    }

    [Fact]
    public void InstalledWorldModelsUseSharedNativePoseForAllOriginalLeaderClips()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the world pose check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        for (int leader = 0; leader < 3; leader++)
        {
            Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, leader);
            Ff7WorldModelAdapter adapter = new(descriptor, descriptor.Load(resources));
            for (int clip = 0; clip < descriptor.Animations.Count; clip++)
            {
                IReadOnlyList<Ff7AppearanceTriangle> triangles = adapter.Create(Vector3.Zero, 0, clip, 0);
                Assert.NotEmpty(triangles);
                Assert.All(triangles, triangle => Assert.True(float.IsFinite(triangle.A.Position.X) && float.IsFinite(triangle.A.Position.Y) && float.IsFinite(triangle.A.Position.Z)));
            }
        }
    }
}
