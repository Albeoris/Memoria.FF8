using System.Collections;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleFramingTests
{
    private static readonly BattleStageCamera Authored = new(new(0, 0, -1000), Vector3.Zero, 240);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeRosterReturnsExactAuthoredCameraWithoutReadingGeometry(int allyCount)
    {
        const short roll = 128;
        ExpandedBattleFrame result = ExpandedBattleFraming.Fit(Authored, roll, new UnreadableVertices(), allyCount, 4);
        Assert.Equal(BattleCamera.FromNative(Authored, roll), result.Camera);
        Assert.Equal(new NativeBattleCameraPose(new(0, 0, -1000), new(0, 0, 0), 240, roll), result.Pose);
        Assert.Equal<short?>(160, result.Projection.OffsetX);
        Assert.Equal<short?>(108, result.Projection.OffsetY);
        AssertCoherent(result);
    }

    [Fact]
    public void FourKeepsExactAuthoredFocalAndCenterWhenBodiesFit()
    {
        Vector3[] bodies = [new(-3, 2, 0), new(3, -2, 0), new(2, 0, 2)];
        ExpandedBattleFrame result = ExpandedBattleFraming.Fit(Authored, 0, bodies, 4, 4);
        Assert.Equal(BattleCamera.FromNative(Authored), result.Camera);
        Assert.Equal<short?>(240, result.Pose.FocalLength);
        Assert.Equal<short?>(108, result.Projection.OffsetY);
        AssertFit(result, bodies, 516f);
    }

    [Fact]
    public void ThreeAlliesAndFiveEnemiesFitWithinTheShortPartyHudMargin()
    {
        Vector3[] bodies = [new(-2, 0, 0), new(2, -3.5f, 0)];
        ExpandedBattleFrame result = ExpandedBattleFraming.Fit(Authored, 0, bodies, 3, 5);
        Assert.Equal<short?>(240, result.Pose.FocalLength);
        Assert.InRange(result.Projection.OffsetY!.Value, (short)1, (short)107);
        AssertFit(result, bodies, 516f);
    }

    [Fact]
    public void ElevenMovesTheProjectionCenterAboveTheHudWithoutWidening()
    {
        Vector3[] bodies = Enumerable.Range(0, 25).SelectMany(index =>
        {
            float x = (index % 5 - 2) * 2;
            float z = (index / 5 - 2) * 0.75f;
            return new Vector3[] { new(x - 0.25f, -2.5f, z), new(x + 0.25f, 0.5f, z) };
        }).ToArray();
        ExpandedBattleFrame result = ExpandedBattleFraming.Fit(Authored, 0, bodies, 11, 14);
        Assert.Equal<short?>(240, result.Pose.FocalLength);
        Assert.InRange(result.Projection.OffsetY!.Value, (short)1, (short)107);
        Assert.Equal(BattleCamera.FromNative(Authored).Eye, result.Camera.Eye);
        Assert.Equal(BattleCamera.FromNative(Authored).Target, result.Camera.Target);
        AssertFit(result, bodies, 408f);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(0, 6)]
    public void LargeProjectedSpansReduceNativeFocalWithoutChangingPose(int x, int y)
    {
        Vector3[] bodies = [new(-x, y, 0), new(x, -y, 0)];
        ExpandedBattleFrame result = ExpandedBattleFraming.Fit(Authored, 0, bodies, 5, 4);
        Assert.InRange(result.Pose.FocalLength!.Value, (short)1, (short)239);
        Assert.Equal(BattleCamera.FromNative(Authored).Eye, result.Camera.Eye);
        Assert.Equal(BattleCamera.FromNative(Authored).Target, result.Camera.Target);
        AssertFit(result, bodies, 408f);
    }

    [Fact]
    public void FractionalCenterIntervalReducesFocalUntilAnIntegerFits()
    {
        BattleStageCamera authored = Authored with { FocalLength = 120 };
        Vector3[] bodies = [new(0, 0.5f / 12, 0), new(0, -137.2f / 12, 0)];
        ExpandedBattleFrame result = ExpandedBattleFraming.Fit(authored, 0, bodies, 5, 4);
        Assert.Equal<short?>(119, result.Pose.FocalLength);
        Assert.Equal<short?>(9, result.Projection.OffsetY);
        AssertFit(result, bodies, 408f);
    }

    [Fact]
    public void LargeAuthoredFocalFindsTheLargestFitAllowedByTheRendererCenter()
    {
        BattleStageCamera authored = Authored with { FocalLength = short.MaxValue };
        Vector3[] bodies = [new(0, -1000, 0)];
        ExpandedBattleFrame result = ExpandedBattleFraming.Fit(authored, 0, bodies, 4, 4);
        Assert.Equal<short?>(1, result.Pose.FocalLength);
        Assert.Equal<short?>(84, result.Projection.OffsetY);
        AssertFit(result, bodies, 516f);
    }

    [Fact]
    public void NonzeroRollAffectsMeasuredSpansAndSurvivesTheFit()
    {
        const short quarterTurn = 1024;
        Vector3[] bodies = [new(-5, 0, 0), new(5, 0, 0)];
        ExpandedBattleFrame horizontal = ExpandedBattleFraming.Fit(Authored, 0, bodies, 4, 4);
        ExpandedBattleFrame rotated = ExpandedBattleFraming.Fit(Authored, quarterTurn, bodies, 4, 4);
        Assert.Equal<short?>(240, horizontal.Pose.FocalLength);
        Assert.InRange(rotated.Pose.FocalLength!.Value, (short)1, (short)239);
        Assert.Equal(quarterTurn, rotated.Camera.NativeRoll);
        AssertFit(rotated, bodies, 516f);
    }

    [Fact]
    public void InvalidGeometryAndFocalReportTheirOwnBoundary()
    {
        Assert.Contains("at least one", Assert.Throws<InvalidDataException>(() => ExpandedBattleFraming.Fit(Authored, 0, [], 4, 4)).Message);
        Assert.Contains("finite", Assert.Throws<InvalidDataException>(() => ExpandedBattleFraming.Fit(Authored, 0, [new(float.NaN, 0, 0)], 4, 4)).Message);
        Assert.Contains("near plane", Assert.Throws<InvalidDataException>(() => ExpandedBattleFraming.Fit(Authored, 0, [new(0, 0, 9.75f)], 4, 4)).Message);
        Assert.Contains("near plane", Assert.Throws<InvalidDataException>(() => ExpandedBattleFraming.Fit(Authored, 0, [new(0, 0, 11)], 4, 4)).Message);
        Assert.Contains("focal length", Assert.Throws<InvalidDataException>(() => ExpandedBattleFraming.Fit(Authored with { FocalLength = 0 }, 0, [Vector3.Zero], 4, 4)).Message);
        Assert.Contains("focal length", Assert.Throws<InvalidDataException>(() => ExpandedBattleFraming.Fit(Authored with { FocalLength = short.MaxValue + 1 }, 0, [Vector3.Zero], 4, 4)).Message);
        Assert.Contains("cannot fit", Assert.Throws<InvalidDataException>(() => ExpandedBattleFraming.Fit(Authored, 0, [new(2000, 0, 0)], 4, 4)).Message);
    }

    private static void AssertFit(ExpandedBattleFrame result, Vector3[] bodies, float safeBottomDesign)
    {
        BattleCamera camera = result.Camera;
        Matrix4x4 roll = Matrix4x4.CreateRotationZ(-camera.NativeRoll * (2 * MathF.PI / 4096));
        Matrix4x4 view = roll * Matrix4x4.CreateLookAt(Vector3.Transform(camera.Eye, roll), Vector3.Transform(camera.Target, roll), Vector3.UnitY);
        float focal = 240 / (2 * MathF.Tan(camera.FieldOfView / 2));
        foreach (Vector3 vertex in bodies)
        {
            Vector3 point = Vector3.Transform(vertex, view);
            Assert.InRange(160 - point.X * focal / point.Z, 8f, 312f);
            Assert.InRange(240 * camera.VerticalCenter + point.Y * focal / point.Z, 8f, safeBottomDesign / 672f * 240);
        }
        AssertCoherent(result);
    }

    private static void AssertCoherent(ExpandedBattleFrame result)
    {
        Assert.Equal(result.Pose.Roll, result.Camera.NativeRoll);
        Assert.Equal((ushort)result.Pose.FocalLength!.Value, result.Projection.FocalLength!.Value);
        Assert.Equal(result.Projection.OffsetY!.Value / 240f, result.Camera.VerticalCenter);
        Assert.Equal(2 * MathF.Atan(120f / result.Pose.FocalLength.Value), result.Camera.FieldOfView);
        NativeBattleActionMatrix view = NativeBattlePoseMatrices.CameraView(result.Pose);
        NativeBattleActionMatrix fittedView = result.Projection.View!;
        Assert.Equal(view.Rotation.ToArray(), fittedView.Rotation.ToArray());
        Assert.Equal((view.X, view.Y, view.Z), (fittedView.X, fittedView.Y, fittedView.Z));
    }

    private sealed class UnreadableVertices : IReadOnlyList<Vector3>
    {
        public int Count => throw new InvalidOperationException();
        public Vector3 this[int index] => throw new InvalidOperationException();
        public IEnumerator<Vector3> GetEnumerator() => throw new InvalidOperationException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
