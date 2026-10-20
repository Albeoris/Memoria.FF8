using System.Numerics;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldPoseInterpolationTests
{
    [Fact]
    public void InterpolationUsesShortestRotationAcrossAngleWrap()
    {
        FieldModel model = CreateModel(new(Vector3.Zero, [new(0, 0, Degrees(350))]), new(Vector3.Zero, [new(0, 0, Degrees(10))]));
        Vector3 midpoint = Assert.Single(model.PoseInterpolated(0, 0, 1, 0.5f));
        AssertVector(Vector3.UnitY, midpoint);
        Assert.Equal(1, midpoint.Length(), 5);
    }

    [Fact]
    public void ExplicitLastToFirstSamplesBlendAcrossLoopBoundary()
    {
        FieldModel model = CreateModel(new(new Vector3(10, 20, 30), [Vector3.Zero]), new(new Vector3(30, 40, 50), [Vector3.Zero]), new(new Vector3(50, 60, 70), [Vector3.Zero]));
        AssertVector(new Vector3(40, 51, 60), Assert.Single(model.PoseInterpolated(0, 2, 3, 0.25f, loop: true)));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(0.5f, 7.5f)]
    [InlineData(1, 10)]
    public void FractionalNativePhasesMoveContinuouslyBetweenSimulationSamples(float blend, float expectedX)
    {
        FieldModel model = CreateModel(new(Vector3.Zero, [Vector3.Zero]), new(new Vector3(10, 0, 0), [Vector3.Zero]));
        AssertVector(new Vector3(expectedX, 1, 0), Assert.Single(model.PoseInterpolated(0, 0.5f, 1, blend)));
    }

    [Fact]
    public void StationaryFractionalPhaseRetainsSmoothPoseAndRootOffset()
    {
        FieldModel model = CreateModel(new(new Vector3(0, 0, 32760), [Vector3.Zero]), new(new Vector3(10, 0, 32760), [Vector3.Zero]));
        AssertVector(new Vector3(5, 1, -32745.5f), Assert.Single(model.PoseInterpolated(0, 0.5f, 0.5f, 1, rootTranslationZ: 30.5f)));
        AssertVector(new Vector3(0, 1, -32745.5f), Assert.Single(model.PoseInterpolated(0, 0, 0, 1, rootTranslationZ: 30.5f)));
    }

    [Fact]
    public void PartialLoopAndReverseWrapUseDeclaredFrameRange()
    {
        FieldModel model = CreateModel(new(new Vector3(-100, 0, 0), [Vector3.Zero]), new(new Vector3(10, 0, 0), [Vector3.Zero]), new(new Vector3(30, 0, 0), [Vector3.Zero]), new(new Vector3(100, 0, 0), [Vector3.Zero]));
        AssertVector(new Vector3(20, 1, 0), Assert.Single(model.PoseInterpolated(0, 1, 0, 0.5f, firstFrame: 1, terminalFrame: 3, loop: true)));
        AssertVector(new Vector3(30, 1, 0), Assert.Single(model.PoseInterpolated(0, 2.5f, 3, 1, firstFrame: 1, terminalFrame: 3)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(1)]
    public void HeldNonLoopingEndpointRemainsExact(float blend)
    {
        FieldModel model = CreateModel(new(Vector3.Zero, [Vector3.Zero]), new(new Vector3(10, 20, 30), [new(0.3f, -1.2f, 2.7f)]));
        Assert.Equal(model.Pose(0, 1), model.PoseInterpolated(0, 1, 1, blend));
    }

    [Fact]
    public void BlendEndpointsAndDiscretePosePreserveExistingSampling()
    {
        FieldModel model = CreateModel(new(Vector3.Zero, [new(0.1f, 0.2f, 0.3f)]), new(new Vector3(10, 20, 30), [new(0.4f, 0.5f, 0.6f)]));
        Assert.Equal(model.Pose(0, 0), model.Pose(0, 0.75f));
        Assert.Equal(model.Pose(0, 0), model.PoseInterpolated(0, 0, 1, 0));
        Assert.Equal(model.Pose(0, 1), model.PoseInterpolated(0, 0, 1, 1));
    }

    [Fact]
    public void RootOffsetUsesNativeTranslationAtBothEndpointsBeforeBlending()
    {
        FieldModel model = CreateModel(new(new Vector3(0, 0, 32760), [Vector3.Zero]), new(new Vector3(0, 0, 32750), [Vector3.Zero]));
        AssertVector(new Vector3(0, 1, -32751), Assert.Single(model.PoseInterpolated(0, 0, 1, 0.5f, rootTranslationZ: 30)));
    }

    [Fact]
    public void ControlOverrideAndSkinTranslationApplyToInterpolatedHierarchy()
    {
        FieldModel model = CreateModel(new(Vector3.Zero, [new(0, 0, 1)]), new(Vector3.Zero, [new(0, 0, 2)])) with { ControlBones = new(0, -1, -1, 0), Vertices = [new(Vector3.UnitY, 0, 7)] };
        AssertVector(new Vector3(0, 3, 0), Assert.Single(model.PoseInterpolated(0, 0, 1, 0.5f, firstControlRotation: Vector3.Zero, skinTranslations: new Dictionary<int, Vector3> { [7] = new(0, 2, 0) })));
    }

    [Fact]
    public void InterpolationPreservesChildBoneLength()
    {
        FieldModel model = new("hierarchy", [new(-1, 10), new(0, 5)], [new(Vector3.Zero, 0), new(Vector3.Zero, 1)], [], [], [new([new(Vector3.Zero, [Vector3.Zero, Vector3.Zero]), new(Vector3.Zero, [new(0, MathF.PI, 0), Vector3.Zero])])]);
        Vector3[] pose = model.PoseInterpolated(0, 0, 1, 0.5f);
        Assert.Equal(10, Vector3.Distance(pose[0], pose[1]), 5);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    public void InvalidBlendFails(float blend)
    {
        FieldModel model = CreateModel(new AnimationFrame(Vector3.Zero, [Vector3.Zero]));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.PoseInterpolated(0, 0, 0, blend));
    }

    private static FieldModel CreateModel(params AnimationFrame[] frames)
    {
        return new("interpolation", [new(-1, 0)], [new(Vector3.UnitY, 0)], [], [], [new(frames)]);
    }

    private static float Degrees(float value)
    {
        return value * MathF.PI / 180;
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }
}
