using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldRenderInterpolationTests
{
    private static readonly WorldSky DefaultSky = new(Vector3.Zero, new Rgba32(0, 0, 0, 255), new Rgba32(40, 40, 40, 255), new Rgba32(80, 80, 80, 255));

    [Fact]
    public void Avatar_wraps_both_world_axes_and_takes_shortest_heading()
    {
        WorldRenderFrame previous = Frame(new Vector3(131060, 20, 98290), MathF.Tau - 0.2f, 3);
        WorldRenderFrame current = Frame(new Vector3(-131060, 40, -98290), 0.2f, 4);
        WorldRenderFrame result = WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        Assert.Equal(new Vector3(-131072, 30, -98304), result.Position);
        Assert.Equal(MathF.Tau, result.Heading, 5);
        Assert.Equal(new WorldAnimationBlend(3, 4, 0.5f), result.AvatarAnimationBlend);
        Assert.Equal(previous.Position, WorldRenderInterpolation.Interpolate(previous, current, 0).Position);
        Assert.Same(current, WorldRenderInterpolation.Interpolate(previous, current, 1));
        Assert.Equal(new Vector3(131060, 20, 98290), previous.Position);
    }

    [Fact]
    public void Model_and_visibility_changes_snap_avatar_pose_and_preserve_scene_transform()
    {
        WorldRenderFrame previous = Frame(new Vector3(10, 0, 0), 0, 3);
        WorldRenderFrame current = Frame(new Vector3(30, 0, 0), 1, 4);
        foreach (WorldRenderFrame changed in new[] { current with { AvatarVisible = false }, current with { AvatarModel = AnimatedModel("other") } })
        {
            WorldRenderFrame result = WorldRenderInterpolation.Interpolate(previous, changed, 0);
            Assert.Equal(previous.Position, result.Position);
            Assert.Equal(previous.Heading, result.Heading);
            Assert.Equal(changed.AvatarVisible, result.AvatarVisible);
            Assert.Same(changed.AvatarModel, result.AvatarModel);
            Assert.Equal(changed.AnimationFrame, result.AnimationFrame);
            Assert.Null(result.AvatarAnimationBlend);
        }
    }

    [Fact]
    public void Avatar_visibility_change_keeps_camera_and_scene_position_in_phase()
    {
        WorldCamera camera = WorldCamera.Foot(0, 4096);
        WorldRenderFrame previous = Frame(new Vector3(10, 0, 0), 0, 3) with { Camera = camera };
        WorldRenderFrame current = Frame(new Vector3(30, 0, 0), 1, 4) with { Camera = camera, AvatarVisible = false };
        WorldRenderFrame result = WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        Assert.Equal(new Vector3(20, 0, 0), result.Position);
        Assert.Equal(0.5f, result.Heading);
        AssertVector(camera.FootFrame(result.Position).Eye, result.Camera.ScriptedFrame!.Value.Eye);
        Assert.False(result.AvatarVisible);
        Assert.Equal(current.AnimationFrame, result.AnimationFrame);
        Assert.Null(result.AvatarAnimationBlend);
    }

    [Fact]
    public void Animation_transition_preserves_avatar_transform_interpolation_and_snaps_only_pose()
    {
        WorldRenderFrame previous = Frame(new Vector3(10, 0, 0), 0, 3);
        WorldRenderFrame current = Frame(new Vector3(30, 0, 0), 1, 0) with { AnimationId = 1 };
        WorldRenderFrame result = WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        Assert.Equal(new Vector3(20, 0, 0), result.Position);
        Assert.Equal(0.5f, result.Heading);
        Assert.Equal(current.AnimationId, result.AnimationId);
        Assert.Equal(current.AnimationFrame, result.AnimationFrame);
        Assert.Null(result.AvatarAnimationBlend);
        Assert.Equal(previous.Position, WorldRenderInterpolation.Interpolate(previous, current, 0).Position);
    }

    [Fact]
    public void Camera_and_entities_keep_a_common_periodic_image_at_the_world_seam()
    {
        WorldStaticModel model = StaticModel();
        WorldCamera camera = WorldCamera.Foot(1024, 4096);
        WorldRenderObjectId id = new(WorldRenderObjectKind.Allocation, 2);
        WorldRenderFrame previous = Frame(new Vector3(131060, 0, 0)) with
        {
            Camera = camera,
            StaticModels = [new NativeWorldStaticModelDraw(model, new NativeWorldStaticModelPose(new Vector3(131000, 0, 0), new NativeWorldPathAngles(0, 0, 0))) { ObjectId = id }]
        };
        WorldRenderFrame current = Frame(new Vector3(-131060, 0, 0)) with
        {
            Camera = camera,
            StaticModels = [new NativeWorldStaticModelDraw(model, new NativeWorldStaticModelPose(new Vector3(-131120, 0, 0), new NativeWorldPathAngles(0, 0, 0))) { ObjectId = id }]
        };
        WorldRenderFrame result = WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        WorldCameraFrame expected = camera.FootFrame(result.Position);
        AssertVector(expected.Eye, result.Camera.ScriptedFrame!.Value.Eye);
        AssertVector(expected.Forward, result.Camera.ScriptedFrame.Value.Forward);
        Assert.Equal(result.Position.X - 60, Assert.Single(result.StaticModels!).Pose.Anchor.X);
        Assert.Equal(previous.Camera, WorldRenderInterpolation.Interpolate(previous, current, 0).Camera);
        Assert.Equal(current.Camera, WorldRenderInterpolation.Interpolate(previous, current, 1).Camera);
    }

    [Fact]
    public void Resolved_scripted_camera_slerps_orientation_and_preserves_optical_basis()
    {
        WorldCamera camera = WorldCamera.Foot(0, 4096);
        WorldRenderFrame previous = Frame(Vector3.Zero) with { Camera = camera with { ScriptedFrame = new WorldCameraFrame(new Vector3(0, 0, -100), Vector3.UnitZ, Vector3.UnitY) } };
        WorldRenderFrame current = Frame(Vector3.Zero) with { Camera = camera with { ScriptedFrame = new WorldCameraFrame(new Vector3(100, 20, 0), Vector3.UnitX, Vector3.UnitY) } };
        WorldCameraFrame result = WorldRenderInterpolation.Interpolate(previous, current, 0.5f).Camera.ScriptedFrame!.Value;
        AssertVector(new Vector3(50, 10, -50), result.Eye);
        AssertVector(Vector3.Normalize(new Vector3(1, 0, 1)), result.Forward);
        AssertVector(Vector3.UnitY, result.Up);
        Assert.Equal(0, Vector3.Dot(result.Forward, result.Up), 5);
    }

    [Fact]
    public void Static_model_identity_matches_across_order_changes_and_slerps_rotation()
    {
        WorldStaticModel model = StaticModel();
        WorldRenderObjectId first = new(WorldRenderObjectKind.Rail, 1);
        WorldRenderObjectId second = new(WorldRenderObjectKind.Rail, 1, 1);
        NativeWorldStaticModelDraw a = new(model, new NativeWorldStaticModelPose(new Vector3(0, 0, 0), new NativeWorldPathAngles(0, 0, 0))) { ObjectId = first };
        NativeWorldStaticModelDraw b = new(model, new NativeWorldStaticModelPose(new Vector3(100, 0, 0), new NativeWorldPathAngles(0, 0, 0))) { ObjectId = second };
        WorldRenderFrame previous = Frame(Vector3.Zero) with { StaticModels = [a, b] };
        WorldRenderFrame current = Frame(Vector3.Zero) with { StaticModels = [b with { Pose = b.Pose with { Anchor = new Vector3(140, 0, 0) } }, a with { Pose = a.Pose with { Anchor = new Vector3(20, 0, 0), Angles = new NativeWorldPathAngles(0, 1024, 0) } }] };
        WorldRenderFrame result = WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        Assert.Equal(120, result.StaticModels![0].Pose.Anchor.X);
        Assert.Equal(10, result.StaticModels[1].Pose.Anchor.X);
        AssertVector(Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateRotationY(MathF.PI / 4)), Vector3.Transform(Vector3.UnitZ, result.StaticModels[1].Pose.Rotation!.Value));
        Assert.Null(previous.StaticModels![0].Pose.Rotation);
        Assert.Null(current.StaticModels![1].Pose.Rotation);
    }

    [Fact]
    public void Animated_models_blend_frames_by_identity_and_snap_changed_animation_or_model()
    {
        FieldModel model = AnimatedModel("first");
        NativeWorldAnimatedModelDraw before = new(model, new Vector3(10, 0, 0), MathF.Tau - 0.2f, 0, 3) { ObjectId = new(WorldRenderObjectKind.Allocation, 4) };
        NativeWorldAnimatedModelDraw after = before with { Position = new Vector3(30, 0, 0), Heading = 0.2f, AnimationFrame = 0 };
        WorldRenderFrame previous = Frame(Vector3.Zero) with { AnimatedModels = [before] };
        WorldRenderFrame current = Frame(Vector3.Zero) with { AnimatedModels = [after] };
        NativeWorldAnimatedModelDraw result = Assert.Single(WorldRenderInterpolation.Interpolate(previous, current, 0.5f).AnimatedModels!);
        Assert.Equal(20, result.Position.X);
        Assert.Equal(MathF.Tau, result.Heading, 5);
        Assert.Equal(new WorldAnimationBlend(3, 0, 0.5f), result.AnimationBlend);
        NativeWorldAnimatedModelDraw changedAnimation = Assert.Single(WorldRenderInterpolation.Interpolate(previous, current with { AnimatedModels = [after with { AnimationId = 1 }] }, 0.5f).AnimatedModels!);
        Assert.Equal(20, changedAnimation.Position.X);
        Assert.Equal(1, changedAnimation.AnimationId);
        Assert.Null(changedAnimation.AnimationBlend);
        foreach (NativeWorldAnimatedModelDraw changed in new[] { after with { Model = AnimatedModel("second") }, after with { ObjectId = null } })
        {
            NativeWorldAnimatedModelDraw snapped = Assert.Single(WorldRenderInterpolation.Interpolate(previous, current with { AnimatedModels = [changed] }, 0.5f).AnimatedModels!);
            Assert.Equal(changed.Position, snapped.Position);
            Assert.Null(snapped.AnimationBlend);
        }
    }

    [Fact]
    public void Forest_geometry_interpolates_only_surviving_same_slot_particles_without_editing_native_state()
    {
        NativeWorldForestParticle first = new(0, 5, 10, -20, -30, 0, 0, 4096, 1, 8, true);
        NativeWorldForestParticle second = first with { X = 30, NativeY = -40, GraphicsZ = -50, Yaw = 1024, Scale = 2048, Age = 2 };
        NativeWorldForestParticle spawned = first with { Slot = 1, X = 100, Age = 1 };
        NativeWorldForestParticle reused = first with { Slot = 2, Age = 0 };
        NativeWorldForestParticle vanished = first with { Slot = 3 };
        NativeWorldForestState before = NativeWorldForestState.Create(77, 0) with { Particles = [first, reused with { Age = 4 }, vanished] };
        NativeWorldForestState after = before with { RandomCursor = 24, Particles = [second, spawned, reused, vanished with { Visible = false }] };
        WorldRenderFrame previous = Frame(Vector3.Zero) with { Forest = before };
        WorldRenderFrame current = Frame(Vector3.Zero) with { Forest = after };
        WorldRenderFrame result = WorldRenderInterpolation.Interpolate(previous, current, 0.5f);
        Assert.Equal(3, result.ForestParticles!.Count);
        WorldForestParticleFrame visual = result.ForestParticles[0];
        AssertVector(new Vector3(20, -30, 40), visual.Position);
        Assert.Equal(3072, visual.Scale);
        Assert.True(visual.Interpolated);
        AssertVector(Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateRotationY(MathF.PI / 4)), Vector3.Transform(Vector3.UnitZ, visual.Rotation));
        Assert.False(result.ForestParticles[1].Interpolated);
        Assert.False(result.ForestParticles[2].Interpolated);
        Assert.Same(after, result.Forest);
        Assert.Equal((byte)77, before.RandomCursor);
        Assert.Equal((byte)24, after.RandomCursor);
        Assert.Equal((ushort)4096, first.Scale);
        Assert.Equal((byte)1, first.Age);
        Assert.Equal(first, WorldRenderInterpolation.Interpolate(previous, current, 0).ForestParticles![0].Particle);
    }

    [Fact]
    public void Captured_frame_copies_collection_topology_and_validates_alpha()
    {
        NativeWorldForestParticle particle = new(0, 5, 0, 0, 0, 0, 0, 4096, 1, 8, true);
        NativeWorldForestParticle[] particles = [particle];
        NativeWorldStaticModelDraw[] models = [new(StaticModel(), new NativeWorldStaticModelPose(Vector3.Zero, new NativeWorldPathAngles(0, 0, 0)))];
        WorldRenderFrame frame = new(Vector3.Zero, 0, 0, 0, WorldCamera.Foot(0, 4096), DefaultSky, StaticModels: models, Forest: NativeWorldForestState.Create(0, 0) with { Particles = particles });
        models[0] = models[0] with { Pose = new NativeWorldStaticModelPose(Vector3.One, new NativeWorldPathAngles(0, 0, 0)) };
        particles[0] = particle with { Scale = 1 };
        Assert.Equal(Vector3.Zero, frame.StaticModels![0].Pose.Anchor);
        Assert.Equal((ushort)4096, frame.Forest!.Particles[0].Scale);
        foreach (float alpha in new[] { -0.1f, 1.1f, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldRenderInterpolation.Interpolate(frame, frame, alpha));
        }
    }

    [Fact]
    public void Original_resource_render_endpoints_preserve_exact_native_pixels()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 80, 56);
        Vector3 position = new(18381, 4000, -68272);
        WorldCamera camera = WorldCamera.Foot(0, 1024);
        NativeWorldForestParticle particle = new(0, 5, 18381, -4000, 68272, 0, 0, 4096, 1, 8, true);
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0) with { Particles = [particle] };
        NativeWorldStaticModelDraw staticModel = new(assets.ExamCar, new NativeWorldStaticModelPose(position + new Vector3(100, 0, 0), new NativeWorldPathAngles(0, 0, 0))) { ObjectId = new(WorldRenderObjectKind.Controlled, 0) };
        WorldRenderFrame previous = new(position, 0, 0, 0, camera, assets.Skies[0], StaticModels: [staticModel], Forest: forest);
        WorldRenderFrame current = new(position + new Vector3(16, 0, 0), 0.02f, 0, 0, camera with { Heading = 0.02f }, assets.Skies[0], StaticModels: [staticModel with { Pose = staticModel.Pose with { Anchor = staticModel.Pose.Anchor + new Vector3(16, 0, 0), Angles = new NativeWorldPathAngles(0, 16, 0) } }], Forest: forest with { Particles = [particle with { X = particle.X + 16, Scale = 3500, Age = 2 }] });
        byte[] first = renderer.Render(previous.Position, previous.Heading, previous.AnimationId, previous.AnimationFrame, previous.Camera, skyOverride: previous.Sky, staticModels: previous.StaticModels, forest: previous.Forest).ToArray();
        byte[] second = renderer.Render(current.Position, current.Heading, current.AnimationId, current.AnimationFrame, current.Camera, skyOverride: current.Sky, staticModels: current.StaticModels, forest: current.Forest).ToArray();
        Assert.Equal(first, renderer.Render(previous));
        Assert.Equal(second, renderer.Render(current));
        Assert.Equal(first, renderer.Render(WorldRenderInterpolation.Interpolate(previous, current, 0)));
        Assert.Equal(second, renderer.Render(WorldRenderInterpolation.Interpolate(previous, current, 1)));
    }

    private static WorldRenderFrame Frame(Vector3 position, float heading = 0, float animationFrame = 0) => new(position, heading, 0, animationFrame, WorldCamera.Foot(0, 4096), DefaultSky);

    private static WorldStaticModel StaticModel() => new(73, 0, 0, [], [], []);

    private static FieldModel AnimatedModel(string name) => new(name, [], [], [], [], []);

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Z, actual.Z, 3);
    }
}
