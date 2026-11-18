using System.Numerics;
using Memoria.FF8.TitleScene;

namespace Memoria.FF8.App.Tests;

public sealed class MeteorMotionTests
{
    [Fact]
    public void OffsetsStayWithinTwoPercentOfTheRadiusForTenMinutes()
    {
        for (int frame = 0; frame <= 600 * 60; frame++)
        {
            Vector2 offset = MeteorMotion.SampleScreenOffset(frame / 60d);
            Assert.InRange(Math.Abs(offset.X), 0, 0.004800001f);
            Assert.InRange(Math.Abs(offset.Y), 0, 0.004800001f);
        }
    }

    [Fact]
    public void SamplingOrderAndPresentationFrameRateDoNotChangeMotion()
    {
        MeteorMotionSample[] atThirtyFrames = new MeteorMotionSample[301];
        for (int frame = 0; frame < atThirtyFrames.Length; frame++)
        {
            atThirtyFrames[frame] = MeteorMotion.Sample(frame / 30d);
        }
        for (int frame = 600; frame >= 0; frame--)
        {
            MeteorMotionSample sample = MeteorMotion.Sample(frame / 60d);
            if (frame % 2 == 0)
            {
                Assert.Equal(atThirtyFrames[frame / 2], sample);
            }
        }
        Assert.Equal(atThirtyFrames[300], MeteorMotion.Sample(10));
    }

    [Theory]
    [InlineData(-0.25)]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    [InlineData(1234567890123)]
    [InlineData(-1234567890123)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void BasisIsOrthonormalAndTracksTwoDegreeSpinWithBoundedTransverseWobble(double seconds)
    {
        MeteorMotionSample motion = MeteorMotion.Sample(seconds);
        Assert.InRange(Math.Abs(motion.ScreenOffset.X), 0, 0.004800001f);
        Assert.InRange(Math.Abs(motion.ScreenOffset.Y), 0, 0.004800001f);
        Assert.InRange(Math.Abs(motion.AxisX.LengthSquared() - 1), 0, 0.000002f);
        Assert.InRange(Math.Abs(motion.AxisY.LengthSquared() - 1), 0, 0.000002f);
        Assert.InRange(Math.Abs(motion.AxisZ.LengthSquared() - 1), 0, 0.000002f);
        Assert.InRange(Math.Abs(Vector3.Dot(motion.AxisX, motion.AxisY)), 0, 0.000002f);
        Assert.InRange(Math.Abs(Vector3.Dot(motion.AxisX, motion.AxisZ)), 0, 0.000002f);
        Assert.InRange(Math.Abs(Vector3.Dot(motion.AxisY, motion.AxisZ)), 0, 0.000002f);
        Assert.InRange(Vector3.Distance(Vector3.Cross(motion.AxisX, motion.AxisY), motion.AxisZ), 0, 0.000002f);
        double radians = seconds % 180 * Math.PI / 90;
        Vector3 expectedY = new(0, (float)Math.Cos(radians), (float)Math.Sin(radians));
        Vector3 expectedZ = new(0, -(float)Math.Sin(radians), (float)Math.Cos(radians));
        Assert.InRange(Vector3.Distance(Vector3.UnitX, motion.AxisX), 0, 0.026181f);
        Assert.InRange(Vector3.Distance(expectedY, motion.AxisY), 0, 0.026181f);
        Assert.InRange(Vector3.Distance(expectedZ, motion.AxisZ), 0, 0.026181f);
        Assert.InRange(Math.Abs(Math.Atan2(motion.AxisX.Y, motion.AxisX.X)), 0, Math.PI / 240 + 0.000001);
        Assert.InRange(Math.Abs(Math.Asin(Math.Clamp(-motion.AxisX.Z, -1, 1))), 0, Math.PI / 240 + 0.000001);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.125)]
    [InlineData(5)]
    [InlineData(-3)]
    [InlineData(1234567890123)]
    public void TailSamplesFollowTheSameHeadPathOverExactlyAQuarterSecond(double seconds)
    {
        Span<Vector2> tail = stackalloc Vector2[9];
        MeteorMotion.FillTailScreenOffsets(seconds, tail);
        for (int index = 0; index < tail.Length; index++)
        {
            Assert.Equal(MeteorMotion.SampleScreenOffset(seconds - 0.25 * index / 8), tail[index]);
        }
        Assert.Equal(MeteorMotion.Sample(seconds).ScreenOffset, tail[0]);
        Assert.Equal(MeteorMotion.Sample(seconds - 0.25).ScreenOffset, tail[8]);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteTimeIsRejected(double seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MeteorMotion.Sample(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeteorMotion.SampleScreenOffset(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeteorMotion.FillTailScreenOffsets(seconds, new Vector2[9]));
    }

    [Fact]
    public void TailRejectsAnIncompleteSampleBuffer()
    {
        Assert.Throws<ArgumentException>(() => MeteorMotion.FillTailScreenOffsets(0, new Vector2[8]));
    }
}
