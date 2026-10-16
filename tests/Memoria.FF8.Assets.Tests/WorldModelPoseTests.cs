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
}
