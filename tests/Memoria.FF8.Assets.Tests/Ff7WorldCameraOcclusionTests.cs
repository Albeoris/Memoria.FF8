using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldCameraOcclusionTests
{
    [Fact]
    public void OriginalProbeUsesStrict160ForBothAxesAndMaximumDepthBeforePositiveWGate()
    {
        Vector3 a = new(140, 150, 5500);
        Vector3 b = new(180, 150, 5500);
        Vector3 c = new(160, 170, 5500);
        Assert.True(Ff7WorldCameraMath.TriangleOccludes(a, b, c));
        Assert.False(Ff7WorldCameraMath.TriangleOccludes(a, c, b));
        Assert.False(Ff7WorldCameraMath.TriangleOccludes(a, b with { Z = 5501 }, c));
        Assert.True(Ff7WorldCameraMath.TriangleOccludes(a, b with { Z = 5500.5f }, c));
        Assert.True(Ff7WorldCameraMath.TriangleOccludes(a with { Z = -100 }, b with { Z = -100 }, c with { Z = -100 }));
        Assert.False(Ff7WorldCameraMath.TriangleOccludes(a with { X = 160 }, b, c));
        Assert.False(Ff7WorldCameraMath.TriangleOccludes(a with { Y = 160 }, b with { Y = 160 }, c));
        Assert.False(Ff7WorldCameraMath.TriangleOccludes(a with { Y = 110 }, b with { Y = 110 }, c with { Y = 130 }));
        Assert.False(Ff7WorldCameraMath.TriangleOccludes(a with { Y = -1 }, b with { Y = -1 }, c with { Y = 241 }));
    }

    [Fact]
    public void InstalledCameraAndTerrainProbeShareOriginalTransformAndDetectTheLowCameraHill()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for world camera occlusion.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldPresentationData data = Ff7PcExecutableData.LoadWorldPresentation(installation, resources);
        Ff7WorldCameraMath camera = new(data);
        Vector3 focus = new(185636, 307, 123325);
        Ff7WorldCameraTransform transform = camera.Create(focus, 3936, 1795, 7029, 307, 0);
        Ff7WorldView render = new Ff7WorldCameraProjection(data).Create(focus, 3936, 1795, 7029, 307, 0, 400, 1720, 667, 2);
        Assert.Equal(transform.View, render.NativeView);
        Assert.Equal(transform.Rotation, render.NativeRotation);
        Matrix4x4.Invert(transform.View, out Matrix4x4 inverse);
        Assert.InRange(inverse.Translation.Y, 2887, 2889);
        IReadOnlyList<Ff7WorldChunkPlacement> chunks = new Ff7WorldVisibility(data).RequiredChunks((int)focus.X, (int)focus.Z, 3936);
        Assert.Contains(chunks, chunk => Ff7WorldCameraMath.Occluded(resources.Map.ReadMesh(chunk.BlockIndex, chunk.MeshIndex), chunk.Origin, transform, 400, 2));
    }
}
