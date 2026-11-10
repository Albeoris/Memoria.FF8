using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldPresentationTests
{
    [Fact]
    public void WorldLeadersReuseCharacterRetargetingAcrossAllElevenAppearances()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Select both original games for world appearance contracts.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using GameAssets assets = new(ff8!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        for (int leader = 0; leader < 3; leader++)
        {
            Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, leader);
            Ff7FieldModelResources original = descriptor.Load(resources);
            Memoria.FF7.Formats.PcFieldModelDescriptor field = new(descriptor.HrcName, 0, descriptor.HrcName, descriptor.ScaleWord.ToString(System.Globalization.CultureInfo.InvariantCulture), [], 0, descriptor.Animations);
            Ff7WorldModelAdapter adapter = new(descriptor, original);
            for (int appearance = 0; appearance < 11; appearance++)
            {
                Ff7ModelAppearance profile = Ff7ModelAppearance.CreateFieldCharacter(assets, appearance, field, original, worldModel: true);
                for (int clip = 0; clip < descriptor.Animations.Count; clip++)
                {
                    int last = original.Animations[descriptor.Animations[clip]].Frames.Count - 1;
                    foreach (int frame in new[] { 0, last })
                    {
                        IReadOnlyList<Ff7WorldModelTriangle> triangles = adapter.Create(new(100, -200, 300), 0x400, clip, frame, profile, leader switch { 0 => 0, 1 => 2, _ => 8 });
                        Assert.NotEmpty(triangles);
                        Assert.All(triangles, triangle => Assert.True(float.IsFinite(triangle.A.Position.X) && float.IsFinite(triangle.A.Position.Y) && float.IsFinite(triangle.A.Position.Z)));
                        Assert.All(triangles, triangle => Assert.Equal(TriangleDepthMapping.Positive, triangle.Material.DepthMapping));
                    }
                }
            }
        }
    }

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
    public void OriginalTerrainCallbackClampsAtZeroWhileModelCallbackRetainsAmbient()
    {
        Assert.Equal(new Vector4(0, 0, 0, 1), Ff7WorldLighting.Terrain(new(-4, -4, -4)));
        Assert.Equal(new Vector4(64f / 255, 64f / 255, 64f / 255, 1), Ff7WorldLighting.Terrain(Vector3.Zero));
        Assert.Equal(new Vector4(64f / 255, 64f / 255, 64f / 255, 0.25f), Ff7WorldLighting.Character(Vector3.Zero, new(1, 1, 1, 0.25f)));
    }

    [Fact]
    public void InstalledWorldModelsUseSharedNativePoseForAllOriginalLeaderClips()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the world pose check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldPresentationData presentation = Ff7PcExecutableData.LoadWorldPresentation(installation, resources);
        Ff7WorldCameraProjection camera = new(presentation);
        Ff7WorldView view = camera.Create(Vector3.Zero, 0, 1500, 10000, 0, 0, 400, 640, 480, 0);
        Ff7WorldTextureResources textures = new(installation, resources);
        Ff7WorldTextureAnimationResources clock = new(installation, resources, textures.Descriptors);
        Ff7WorldTerrainAdapter terrain = new(textures);
        Capture first = new();
        var sourceMesh = resources.Map.ReadMesh(0, 0);
        int count = terrain.DrawNative(first, sourceMesh, Vector3.Zero, view, id => clock.Frame(id, 0));
        Assert.True(count > 0);
        Capture second = new();
        terrain.DrawNative(second, sourceMesh, Vector3.Zero, view, id => clock.Frame(id, 0));
        Assert.Equal(first.Vertices, second.Vertices);
        Ff7WorldTerrainAdapter cold = new(new Ff7WorldTextureResources(installation, resources));
        Capture restored = new();
        cold.DrawNative(restored, resources.Map.ReadMesh(0, 0), Vector3.Zero, camera.Create(Vector3.Zero, 0, 1500, 10000, 0, 0, 400, 640, 480, 0), id => clock.Frame(id, 0));
        Assert.Equal(first.Vertices, restored.Vertices);
        int maxVertices = 0;
        for (int block = 0; block < resources.Map.BlockCount; block++)
        {
            for (int mesh = 0; mesh < 16; mesh++)
            {
                maxVertices = Math.Max(maxVertices, resources.Map.ReadMesh(block, mesh).Vertices.Count);
            }
        }
        Assert.InRange(maxVertices, 1, 121);
        for (int leader = 0; leader < 3; leader++)
        {
            Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, leader);
            Ff7FieldModelResources model = descriptor.Load(resources);
            Ff7WorldModelAdapter adapter = new(descriptor, model);
            for (int clip = 0; clip < descriptor.Animations.Count; clip++)
            {
                IReadOnlyList<Ff7WorldModelTriangle> triangles = adapter.Create(Vector3.Zero, 0, clip, 0);
                Assert.NotEmpty(triangles);
                Assert.All(triangles, triangle => Assert.True(float.IsFinite(triangle.A.Position.X) && float.IsFinite(triangle.A.Position.Y) && float.IsFinite(triangle.A.Position.Z)));
                if (clip == 0)
                {
                    Capture modelCapture = new();
                    Assert.True(Ff7WorldModelAdapter.Draw(modelCapture, triangles, view) > 0);
                    Assert.All(modelCapture.Vertices, vertex => Assert.True(float.IsFinite(vertex.Position.X) && float.IsFinite(vertex.Position.Y) && vertex.ReciprocalW > 0));
                }
                Assert.NotEmpty(adapter.Create(Vector3.Zero, 0, clip, model.Animations[descriptor.Animations[clip]].Frames.Count - 1));
            }
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = new();

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
        }

        public void End()
        {
        }
    }
}
