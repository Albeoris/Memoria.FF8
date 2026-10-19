using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldModelPoseTests
{
    [Fact]
    public void SelectedOriginalWorldSquallIdleKeepsFeetNearGroundAndBodyUpright()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        Vector3[] pose = WorldModelPose.Pose(assets.Squall, 0, 0, MathF.PI);
        Assert.InRange(pose.Min(point => point.Y), -9.1f, -8.8f);
        Assert.InRange(pose.Max(point => point.Y), 225.4f, 225.7f);
        Assert.InRange(pose.Max(point => point.Z) - pose.Min(point => point.Z), 49.5f, 50);
    }

    [Fact]
    public void NativeBoneRotationKeepsColumnRxRyRzOrderWithoutAnExtraTranspose()
    {
        FieldModel model = new("noncommuting native axes", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(1, 2, 3), 0)], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [new Vector3(MathF.PI / 2, 0, MathF.PI / 2)])])]);
        Vector3 point = Assert.Single(WorldModelPose.Pose(model, 0, 0, MathF.PI));
        Assert.InRange(Vector3.Distance(new Vector3(3, 2, -1), point), 0, 0.0001f);
    }

    [Fact]
    public void WorldVirtualRootTranslationAndChildAnchorUseOriginalWorldBasis()
    {
        FieldModel model = new("native basis", [new ModelBone(-1, 40), new ModelBone(0, 0)], [new ModelVertex(new Vector3(10, 0, 0), 0), new ModelVertex(Vector3.Zero, 1)], [], [], [new ModelAnimation([new AnimationFrame(new Vector3(2, 5, 154), [Vector3.Zero, Vector3.Zero])])]);
        Vector3[] pose = WorldModelPose.Pose(model, 0, 0, MathF.PI);
        Assert.InRange(Vector3.Distance(new Vector3(2, 144, -5), pose[0]), 0, 0.0001f);
        Assert.InRange(Vector3.Distance(new Vector3(2, 154, -45), pose[1]), 0, 0.0001f);
        Vector3[] opposite = WorldModelPose.Pose(model, 0, 0, 0);
        Assert.InRange(Vector3.Distance(new Vector3(-2, 144, 5), opposite[0]), 0, 0.0001f);
    }

    [Fact]
    public void NativeBonePackingDropsTwoLowAngleBitsBeforeWorldPose()
    {
        FieldModel model = new("packed angles", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(40, 20, 10), 0)], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [new Vector3(1027, 515, 259) * (MathF.Tau / 4096)]), new AnimationFrame(Vector3.Zero, [new Vector3(1024, 512, 256) * (MathF.Tau / 4096)])])]);
        Assert.Equal(WorldModelPose.Pose(model, 0, 1, MathF.PI), WorldModelPose.Pose(model, 0, 0, MathF.PI));
    }

    [Fact]
    public void InterpolatedLoopEndpointsMatchNativePosesExactlyAndLeaveFramesUnchanged()
    {
        FieldModel model = new("loop endpoints", [new ModelBone(-1, 40), new ModelBone(0, 0)], [new ModelVertex(new Vector3(40, 20, 10), 0), new ModelVertex(new Vector3(10, 5, 2), 1)], [], [], [new ModelAnimation([new AnimationFrame(new Vector3(2, 5, 154), [new Vector3(1, 2, 3), new Vector3(3, 1, 2)]), new AnimationFrame(new Vector3(12, 15, 164), [new Vector3(2, 3, 1), new Vector3(1, 3, 2)])])]);
        Vector3[] first = WorldModelPose.Pose(model, 0, 0, 0.75f);
        Vector3[] last = WorldModelPose.Pose(model, 0, 1, 0.75f);
        Assert.Equal(last, WorldModelPose.PoseInterpolated(model, 0, 1, 0, 0, 0.75f));
        Assert.Equal(first, WorldModelPose.PoseInterpolated(model, 0, 1, 0, 1, 0.75f));
        Assert.Equal(first, WorldModelPose.PoseInterpolated(model, 0, 0, 0, 0.5f, 0.75f));
        WorldModelPose.PoseInterpolated(model, 0, 1, 0, 0.5f, 0.75f);
        Assert.Equal(first, WorldModelPose.Pose(model, 0, 0, 0.75f));
        Assert.Equal(last, WorldModelPose.Pose(model, 0, 1, 0.75f));
    }

    [Fact]
    public void InterpolatedBoneRotationKeepsChildAnchorOnItsArc()
    {
        FieldModel model = new("child anchor arc", [new ModelBone(-1, 40), new ModelBone(0, 0)], [new ModelVertex(Vector3.Zero, 1)], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero, Vector3.Zero]), new AnimationFrame(Vector3.Zero, [new Vector3(MathF.PI / 2, 0, 0), Vector3.Zero])])]);
        Vector3 point = Assert.Single(WorldModelPose.PoseInterpolated(model, 0, 0, 1, 0.5f, MathF.PI));
        float diagonal = 40 / MathF.Sqrt(2);
        Assert.InRange(Vector3.Distance(new Vector3(diagonal, 0, -diagonal), point), 0, 0.0001f);
        Assert.InRange(point.Length(), 39.9999f, 40.0001f);
    }

    [Fact]
    public void InterpolatedBoneRotationTakesShortestArcAcrossPackedAngleWrap()
    {
        float angle = 1984 * (MathF.Tau / 4096);
        FieldModel model = new("angle wrap", [new ModelBone(-1, 40), new ModelBone(0, 0)], [new ModelVertex(Vector3.Zero, 1)], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [new Vector3(angle, 0, 0), Vector3.Zero]), new AnimationFrame(Vector3.Zero, [new Vector3(-angle, 0, 0), Vector3.Zero])])]);
        Vector3 point = Assert.Single(WorldModelPose.PoseInterpolated(model, 0, 0, 1, 0.5f, MathF.PI));
        Assert.InRange(Vector3.Distance(new Vector3(0, 0, 40), point), 0, 0.0001f);
    }

    [Theory]
    [InlineData(MathF.PI, 4, -8)]
    [InlineData(0, -4, 8)]
    public void InterpolatedRootTranslationUsesWorldBasisAndAppliesHeadingOnce(float heading, float expectedX, float expectedZ)
    {
        FieldModel model = new("root translation", [new ModelBone(-1, 0)], [new ModelVertex(Vector3.Zero, 0)], [], [], [new ModelAnimation([new AnimationFrame(new Vector3(2, 5, 154), [Vector3.Zero]), new AnimationFrame(new Vector3(10, 17, 170), [Vector3.Zero])])]);
        Vector3 point = Assert.Single(WorldModelPose.PoseInterpolated(model, 0, 0, 1, 0.25f, heading));
        Assert.InRange(Vector3.Distance(new Vector3(expectedX, 158, expectedZ), point), 0, 0.0001f);
    }

    [Fact]
    public void InterpolatedRotationsDropPackedLowBitsBeforeBlending()
    {
        float unit = MathF.Tau / 4096;
        FieldModel model = new("packed interpolation", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(40, 20, 10), 0)], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [new Vector3(1027, 515, 259) * unit]), new AnimationFrame(Vector3.Zero, [new Vector3(1507, 771, 515) * unit]), new AnimationFrame(Vector3.Zero, [new Vector3(1024, 512, 256) * unit]), new AnimationFrame(Vector3.Zero, [new Vector3(1504, 768, 512) * unit])])]);
        Assert.Equal(WorldModelPose.PoseInterpolated(model, 0, 2, 3, 0.3f, MathF.PI), WorldModelPose.PoseInterpolated(model, 0, 0, 1, 0.3f, MathF.PI));
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void InterpolationRejectsInvalidBlend(float blend)
    {
        FieldModel model = new("invalid blend", [new ModelBone(-1, 0)], [new ModelVertex(Vector3.Zero, 0)], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldModelPose.PoseInterpolated(model, 0, 0, 0, blend, 0));
    }
}
