using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldPresentationTests
{
    [Fact]
    public void OriginalWorldBasisKeepsNativeUpAndComposesVehiclePitchBankAndHeading()
    {
        Assert.Equal(Matrix4x4.Identity, Ff7WorldModelAdapter.Rotation(0, Vector3.Zero));
        Vector3 tilted = Vector3.TransformNormal(Vector3.UnitZ, Ff7WorldModelAdapter.Rotation(0, new(64, 0, 0)));
        Assert.InRange(Vector3.Distance(-Vector3.UnitY, tilted), 0, 0.000001f);
        Vector3 turned = Vector3.TransformNormal(Vector3.UnitZ, Ff7WorldModelAdapter.Rotation(1024, new(0, 1024, 0)));
        Assert.InRange(Vector3.Distance(-Vector3.UnitZ, turned), 0, 0.000001f);
        Vector3 banked = Vector3.TransformNormal(Vector3.UnitZ, Ff7WorldModelAdapter.Rotation(1024, new(64, 0, 1024)));
        Assert.InRange(Vector3.Distance(-Vector3.UnitZ, banked), 0, 0.000001f);
    }

    [Fact]
    public void InstalledResourceProfilesBindAndRenderEveryOriginalWorldModel()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for world model profiles.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        HashSet<Ff7WorldCharacterDescriptor> checkedDescriptors = [];
        int[] expectedCounts = [13, 16, 16, 16, 16, 16, 16, 15, 15, 5, 5, 10, 6];
        for (int area = 0; area <= 12; area++)
        {
            int count = 0;
            for (int model = 0; model < 32; model++)
            {
                if (!Ff7PcExecutableData.TryLoadWorldCharacterDescriptor(installation, model, out Ff7WorldCharacterDescriptor? descriptor, resourceArea: area))
                {
                    continue;
                }
                count++;
                Assert.NotNull(descriptor);
                if (!checkedDescriptors.Add(descriptor))
                {
                    continue;
                }
                Ff7FieldModelResources original = descriptor.Load(resources);
                Ff7WorldModelAdapter adapter = new(descriptor, original);
                for (int clip = 0; clip < descriptor.Animations.Count; clip++)
                {
                    int last = original.Animations[descriptor.Animations[clip]].Frames.Count - 1;
                    IReadOnlyList<Ff7WorldModelTriangle> triangles = adapter.Create(Vector3.Zero, 0, clip, 0, animationBlend: new(last, 0, 0.5f));
                    Assert.NotEmpty(triangles);
                    Assert.All(triangles, triangle => Assert.True(float.IsFinite(triangle.A.Position.X) && float.IsFinite(triangle.A.Position.Y) && float.IsFinite(triangle.A.Position.Z)));
                }
            }
            Assert.Equal(expectedCounts[area], count);
        }
        Assert.Equal("cgd.hrc", Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 3, resourceArea: 1).HrcName);
        Assert.Equal("cid.hrc", Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 3, resourceArea: 8).HrcName);
        Assert.Equal("dic.hrc", Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 27, world: 2).HrcName);
        Assert.Equal("dga.hrc", Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 28, world: 2).HrcName);
        Assert.False(Ff7PcExecutableData.TryLoadWorldCharacterDescriptor(installation, 29, out _, world: 2));
        Assert.Equal("djc.hrc", Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 21, world: 3).HrcName);
        Assert.Equal(((short)-30, (short)50, (short)50), Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 19, tint: 2, resourceArea: 1).ColorOffset);
        Assert.Equal(((short)0, (short)-255, (short)-255), Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 19, tint: 5, resourceArea: 1).ColorOffset);
    }

    [Fact]
    public void InstalledWorldAnimationBlendsPreserveEndpointsAndSmoothLoopBoundaries()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for world animation interpolation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        bool movingPose = false;
        for (int leader = 0; leader < 3; leader++)
        {
            Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, leader);
            Ff7FieldModelResources original = descriptor.Load(resources);
            Ff7WorldModelAdapter adapter = new(descriptor, original);
            for (int clip = 0; clip < descriptor.Animations.Count; clip++)
            {
                int last = original.Animations[descriptor.Animations[clip]].Frames.Count - 1;
                IReadOnlyList<Ff7WorldModelTriangle> first = adapter.Create(Vector3.Zero, 0, clip, last);
                IReadOnlyList<Ff7WorldModelTriangle> second = adapter.Create(Vector3.Zero, 0, clip, 0);
                if (clip == 0)
                {
                    Vector3[] vertices = second.SelectMany(triangle => new[] { triangle.A.Position, triangle.B.Position, triangle.C.Position }).ToArray();
                    float height = vertices.Max(vertex => vertex.Y) - vertices.Min(vertex => vertex.Y);
                    float depth = vertices.Max(vertex => vertex.Z) - vertices.Min(vertex => vertex.Z);
                    Assert.True(height > depth, $"Original world leader {leader} must stand on its native Y axis (height {height}, depth {depth}).");
                    Assert.True(vertices.Average(vertex => vertex.Y) > 0, $"Original world leader {leader} must extend above the ground on positive native Y.");
                }
                Assert.Equal(first, adapter.Create(Vector3.Zero, 0, clip, 0, animationBlend: new(last, 0, 0)));
                Assert.Equal(second, adapter.Create(Vector3.Zero, 0, clip, 0, animationBlend: new(last, 0, 1)));
                IReadOnlyList<Ff7WorldModelTriangle> blended = adapter.Create(Vector3.Zero, 0, clip, 0, animationBlend: new(last, 0, 0.5f));
                Assert.Equal(second.Count, blended.Count);
                Assert.All(blended, triangle => Assert.True(float.IsFinite(triangle.A.Position.X) && float.IsFinite(triangle.A.Position.Y) && float.IsFinite(triangle.A.Position.Z)));
                movingPose |= blended.Zip(second).Any(pair => Vector3.Distance(pair.First.A.Position, pair.Second.A.Position) > 0.001f);
            }
        }
        Assert.True(movingPose);
    }

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
                        if (clip == 0 && frame == 0)
                        {
                            Vector3[] vertices = triangles.SelectMany(triangle => new[] { triangle.A.Position, triangle.B.Position, triangle.C.Position }).ToArray();
                            float height = vertices.Max(vertex => vertex.Y) - vertices.Min(vertex => vertex.Y);
                            float depth = vertices.Max(vertex => vertex.Z) - vertices.Min(vertex => vertex.Z);
                            Assert.True(height > depth, $"World leader {leader} appearance {appearance} must stand on its native Y axis.");
                            Assert.True(vertices.Average(vertex => vertex.Y) > -200, $"World leader {leader} appearance {appearance} must extend above its native ground anchor.");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void InstalledYellowChocoboUsesOriginalPosesAndTint()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the yellow Chocobo pose check.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 4);
        Assert.Equal(((sbyte)50, (sbyte)50, (sbyte)-30), descriptor.ColorOffset);
        Ff7FieldModelResources original = descriptor.Load(resources);
        Ff7WorldModelAdapter tinted = new(descriptor, original);
        Ff7WorldModelAdapter untinted = new(descriptor with { ColorOffset = null }, original);
        for (int clip = 0; clip < descriptor.Animations.Count; clip++)
        {
            IReadOnlyList<Ff7WorldModelTriangle> pose = tinted.Create(new(100, -200, 300), 256, clip, 0);
            IReadOnlyList<Ff7WorldModelTriangle> basePose = untinted.Create(new(100, -200, 300), 256, clip, 0);
            Assert.NotEmpty(pose);
            Assert.Equal(basePose.Count, pose.Count);
            Assert.Contains(pose.Zip(basePose), pair => pair.First.A.Color != pair.Second.A.Color);
            Assert.All(pose, triangle => Assert.True(float.IsFinite(triangle.A.Position.X) && float.IsFinite(triangle.A.Position.Y) && float.IsFinite(triangle.A.Position.Z)));
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
        Ff7WorldTerrainReference terrain = new(textures);
        Capture first = new();
        var sourceMesh = resources.Map.ReadMesh(0, 0);
        int count = terrain.DrawNative(first, sourceMesh, Vector3.Zero, view, id => clock.Frame(id, 0));
        Assert.True(count > 0);
        Capture second = new();
        terrain.DrawNative(second, sourceMesh, Vector3.Zero, view, id => clock.Frame(id, 0));
        Assert.Equal(first.Vertices, second.Vertices);
        Ff7WorldTerrainReference cold = new(new Ff7WorldTextureResources(installation, resources));
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
