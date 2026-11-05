using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldShadowInterpolationTests
{
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void Nonfinite_presentation_yaw_is_rejected(float yaw)
    {
        NativeWorldShadowDraw draw = Shadow(Vector3.Zero, 0, 0, 1) with { PresentationYaw = yaw };
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldShadowPresentation.Create(draw, new NativeWorldContactShape(128, 64, 300, 1)));
    }

    [Fact]
    public void Matching_shadows_interpolate_wrapped_anchors_shortest_yaw_ground_and_scale()
    {
        NativeWorldShadowDraw first = Shadow(new(131070, 100, 98302), 4095, 10, 0.75f);
        NativeWorldShadowDraw last = Shadow(new(-131070, 200, -98302), 0, 30, 1);
        WorldRenderFrame before = Frame([first]) with { Position = first.Anchor };
        WorldRenderFrame after = Frame([last]) with { Position = last.Anchor };
        string original = JsonSerializer.Serialize(new[] { before, after });
        NativeWorldShadowDraw result = Assert.Single(WorldRenderInterpolation.Interpolate(before, after, 0.25f).Shadows!);
        Assert.Equal(new Vector3(131071, 125, 98303), result.Anchor);
        Assert.Equal(4095.25f, result.PresentationYaw);
        Assert.Equal(15, result.GroundHeight);
        Assert.Equal(0.8125f, result.Scale);
        Assert.Equal(last.NativeYaw, result.NativeYaw);
        Assert.Equal(last.ObjectId, result.ObjectId);
        Assert.Equal(original, JsonSerializer.Serialize(new[] { before, after }));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Endpoints_preserve_exact_native_draws_and_geometry(float fraction)
    {
        NativeWorldShadowDraw first = Shadow(new(10, 20, 30), 4095, 10, 0.75f);
        NativeWorldShadowDraw last = Shadow(new(30, 40, 50), 0, 30, 1);
        NativeWorldShadowDraw result = Assert.Single(WorldRenderInterpolation.Interpolate(Frame([first]), Frame([last]), fraction).Shadows!);
        NativeWorldShadowDraw expected = fraction == 0 ? first : last;
        Assert.Equal(expected, result);
        NativeWorldContactShape shape = new(128, 64, 300, 1);
        Assert.Equal(WorldShadowPresentation.Create(expected, shape).Triangles, WorldShadowPresentation.Create(result, shape).Triangles);
    }

    [Fact]
    public void Fractional_shadow_yaw_reaches_geometry_with_native_shape_correction()
    {
        NativeWorldShadowDraw first = Shadow(new(10, 9999, 20), 100, 10, 1);
        NativeWorldShadowDraw last = first with { NativeYaw = 101 };
        NativeWorldShadowDraw result = Assert.Single(WorldRenderInterpolation.Interpolate(Frame([first]), Frame([last]), 0.25f).Shadows!);
        Assert.Equal(100.25f, result.PresentationYaw);
        WorldShadowGeometry geometry = WorldShadowPresentation.Create(result, new NativeWorldContactShape(128, 64, 300, 1));
        short correction = WorldStaticModelTransform.AllocationAngles(first.ShapeId, 0, 0, 0).Yaw;
        Vector3 rotated = Vector3.Transform(new Vector3(64, 0, 32), Matrix4x4.CreateRotationY((100.25f + correction) * MathF.Tau / 4096));
        Vector3 expected = new(first.Anchor.X + rotated.X, 10, first.Anchor.Z - rotated.Z);
        Assert.Equal(expected, geometry.Triangles[0].A.Position);
        Assert.NotEqual(WorldShadowPresentation.Create(last, new NativeWorldContactShape(128, 64, 300, 1)).Triangles[0].A.Position, geometry.Triangles[0].A.Position);
    }

    [Theory]
    [InlineData("birth")]
    [InlineData("shape")]
    [InlineData("missing identity")]
    [InlineData("different group")]
    [InlineData("ambiguous identity")]
    public void New_or_unmatched_shadow_owners_snap_to_current_geometry(string change)
    {
        NativeWorldShadowDraw first = Shadow(new(10, 20, 30), 100, 10, 0.75f);
        NativeWorldShadowDraw last = Shadow(new(30, 40, 50), 200, 30, 1);
        NativeWorldShadowDraw[] previous = [first];
        switch (change)
        {
            case "birth":
                previous = [];
                break;
            case "shape":
                last = last with { ShapeId = 0 };
                break;
            case "missing identity":
                last = last with { ObjectId = null };
                break;
            case "different group":
                last = last with { ObjectId = new(WorldRenderObjectKind.Allocation, 3, 1) };
                break;
            case "ambiguous identity":
                previous = [first, first];
                break;
        }
        Assert.Equal(last, Assert.Single(WorldRenderInterpolation.Interpolate(Frame(previous), Frame([last]), 0.5f).Shadows!));
        Assert.Empty(WorldRenderInterpolation.Interpolate(Frame([first]), Frame([]), 0.5f).Shadows!);
    }

    [Fact]
    public void Stable_identity_matches_shadows_across_list_reordering()
    {
        NativeWorldShadowDraw first = Shadow(new(10, 0, 0), 0, 0, 1);
        NativeWorldShadowDraw other = first with { ObjectId = new(WorldRenderObjectKind.Avatar, 0), Anchor = new(100, 0, 0) };
        NativeWorldShadowDraw last = first with { Anchor = new(30, 0, 0) };
        NativeWorldShadowDraw[] result = WorldRenderInterpolation.Interpolate(Frame([first, other]), Frame([other, last]), 0.5f).Shadows!.ToArray();
        Assert.Equal(other.ObjectId, result[0].ObjectId);
        Assert.Equal(100, result[0].Anchor.X);
        Assert.Equal(first.ObjectId, result[1].ObjectId);
        Assert.Equal(20, result[1].Anchor.X);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restored_or_nonconsecutive_world_callbacks_clear_shadow_history(bool restore)
    {
        WorldPresentation presentation = new();
        WorldState first = new() { Frame = 4, X = 10, BodyYaw = 100 };
        WorldState last = first with { Frame = 5, X = 30, BodyYaw = 200 };
        presentation.Sample(first, 1, Prepare);
        presentation.Commit(first, last, Prepare);
        Assert.Equal(20, Assert.Single(presentation.Sample(last, 0.5f, Prepare).Shadows!).Anchor.X);
        if (restore)
        {
            last = last with { X = 5000 };
        }
        else
        {
            WorldState skipped = last with { Frame = 10, X = 5000 };
            presentation.Commit(last, skipped, Prepare);
            last = skipped;
        }
        NativeWorldShadowDraw result = Assert.Single(presentation.Sample(last, 0.5f, Prepare).Shadows!);
        Assert.Equal(5000, result.Anchor.X);
        Assert.Null(result.PresentationYaw);
    }

    private static NativeWorldShadowDraw Shadow(Vector3 position, short yaw, float ground, float scale)
    {
        return new NativeWorldShadowDraw(73, position, yaw, ground, scale) { ObjectId = new(WorldRenderObjectKind.Allocation, 3) };
    }

    private static WorldRenderFrame Frame(IReadOnlyList<NativeWorldShadowDraw> shadows)
    {
        WorldSky sky = new(Vector3.Zero, new Rgba32(0, 0, 0), new Rgba32(0, 0, 0), new Rgba32(0, 0, 0));
        return new WorldRenderFrame(Vector3.Zero, 0, 0, 0, WorldCamera.Foot(0, 2048), sky, false) { Shadows = shadows };
    }

    private static WorldRenderFrame Prepare(WorldState state)
    {
        return Frame([Shadow(new Vector3(state.X, 0, state.Z), checked((short)state.BodyYaw), -state.NativeHeight, 1)]);
    }
}
