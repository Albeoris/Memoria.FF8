using System.Numerics;
using Memoria.FF8.TitleScene;

namespace Memoria.FF8.App.Tests;

public sealed class MeteorMotionTests
{
    [Fact]
    public void TwoMinuteChaseHasFortySecondAdvanceTwentySecondRecoilAndSixtySecondReturn()
    {
        Assert.Equal(120, MeteorMotion.CycleSeconds);
        Assert.Equal(40, MeteorMotion.AdvanceSeconds);
        Assert.Equal(20, MeteorMotion.RecoilSeconds);
        Assert.Equal(3 * 0.06 * MeteorMotion.Radius, MeteorMotion.ForwardDistance, 12);
        Assert.Equal(3 * 0.10 * MeteorMotion.Radius, MeteorMotion.BackwardDistance, 12);
        Assert.Equal(Vector2.Zero, MeteorMotion.SampleScreenOffset(0));
        Assert.InRange(Math.Abs(FlightPosition(40) + 0.18 * 0.24), 0, 0.00000002);
        Assert.InRange(Math.Abs(FlightPosition(60) - 0.30 * 0.24), 0, 0.00000002);
        Assert.Equal(Vector2.Zero, MeteorMotion.SampleScreenOffset(120));
        Assert.True(FlightPosition(20) < 0);
        Assert.True(FlightPosition(90) > 0);
        Assert.True(MeteorMotion.SampleScreenOffset(40).Y > 0);
        Assert.True(MeteorMotion.SampleScreenOffset(60).Y < 0);
        for (int second = -240; second <= 360; second++)
        {
            Assert.Equal(MeteorMotion.SampleScreenOffset(second), MeteorMotion.SampleScreenOffset(second + 120));
        }
    }

    [Fact]
    public void AdvanceAcceleratesForTwentySecondsAndReturnSlowsGraduallyOverItsLastThirtySeconds()
    {
        float previousSpeed = 0;
        for (int second = 2; second <= 20; second += 2)
        {
            float speed = -FlightVelocity(second);
            Assert.True(speed > previousSpeed);
            previousSpeed = speed;
        }
        Assert.True(-FlightVelocity(90) > -FlightVelocity(100));
        Assert.True(-FlightVelocity(100) > -FlightVelocity(110));
        Assert.True(-FlightVelocity(110) > -FlightVelocity(118));
        Assert.True(FlightPosition(90) > FlightPosition(105));
        Assert.True(FlightPosition(105) > FlightPosition(119));
        Assert.True(FlightPosition(119) > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(120)]
    public void TranslationStopsSmoothlyAtEveryTurnAndTheCycleSeam(double seconds)
    {
        const double step = 0.1;
        double center = FlightPosition(seconds);
        double before = FlightPosition(seconds - step);
        double after = FlightPosition(seconds + step);
        Assert.InRange(Math.Abs((after - before) / (2 * step)), 0, 0.000002);
        Assert.InRange(Math.Abs((after - 2 * center + before) / (step * step)), 0, 0.00005);
    }

    [Fact]
    public void FlameBoostPeaksAtTheForwardTurnAndReturnsToTheSharedBaselineDuringReturn()
    {
        Assert.Equal(1.3f, MeteorMotion.SampleFlameIntensity(40));
        Assert.Equal(1.3f, MeteorMotion.SampleFlameIntensity(160));
        foreach (double seconds in new[] { -60d, 60, 75, 90, 110, 120 })
        {
            float baseline = (float)(1 + 0.055 * Math.Sin(seconds * 3.2) + 0.025 * Math.Sin(seconds * 7.1));
            Assert.InRange(Math.Abs(MeteorMotion.SampleFlameIntensity(seconds) - baseline), 0, 0.000001f);
        }
        for (int frame = 0; frame <= 120 * 60; frame++)
        {
            double seconds = frame / 60d;
            Assert.InRange(MeteorMotion.SampleFlameIntensity(seconds), 0.92f, 1.3f);
        }
        Assert.InRange(MeteorMotion.SampleFlameIntensity(30), 1.25f, 1.3f);
        Assert.InRange(MeteorMotion.SampleFlameIntensity(45), 1.25f, 1.3f);
        Assert.InRange(MeteorMotion.SampleFlameIntensity(double.MaxValue), 0.92f, 1.3f);
        Assert.InRange(MeteorMotion.SampleFlameIntensity(double.MinValue), 0.92f, 1.3f);
    }

    [Fact]
    public void AngularImpulsesRemainRareAndFinishBeforeTheNextEventIncludingTheCycleSeam()
    {
        for (int slot = -1; slot < MeteorMotion.ImpulseSlotCount; slot++)
        {
            MeteorMotionImpulse impulse = MeteorMotion.DescribeImpulse(slot);
            MeteorMotionImpulse next = MeteorMotion.DescribeImpulse(slot + 1);
            Assert.InRange(next.StartSeconds - impulse.StartSeconds, 2, 5);
            Assert.InRange(impulse.AngularDurationSeconds, 0.4, 0.8);
            Assert.InRange(impulse.PeakTiltRadians * 180 / Math.PI, 0.15, 0.30000001);
            Assert.True(impulse.StartSeconds + impulse.AngularDurationSeconds < next.StartSeconds);
            Assert.InRange(ReadTilt(MeteorMotion.Sample(impulse.StartSeconds + impulse.AngularDurationSeconds + 0.01)).Length(), 0, 0.0000001f);
        }
        Assert.InRange(ReadTilt(MeteorMotion.Sample(-0.01)).Length(), 0, 0.0000001f);
        Assert.InRange(ReadTilt(MeteorMotion.Sample(0)).Length(), 0, 0.0000001f);
    }

    [Fact]
    public void EachTiltHasOnePeakThenSettlesWithoutReversingOrOvershooting()
    {
        for (int slot = 0; slot < 128; slot++)
        {
            MeteorMotionImpulse impulse = MeteorMotion.DescribeImpulse(slot);
            float previous = 0;
            for (int step = 0; step <= 80; step++)
            {
                double seconds = impulse.StartSeconds + impulse.AngularDurationSeconds * step / 80;
                Vector2 tilt = ReadTilt(MeteorMotion.Sample(seconds));
                float magnitude = Vector2.Dot(tilt, impulse.TiltDirection);
                Assert.InRange(magnitude, -0.0000001f, (float)impulse.PeakTiltRadians + 0.0000001f);
                Assert.InRange(Vector2.Distance(tilt, impulse.TiltDirection * magnitude), 0, 0.0000001f);
                if (step <= 10)
                {
                    Assert.True(magnitude >= previous - 0.0000001f);
                }
                else
                {
                    Assert.True(magnitude <= previous + 0.0000001f);
                }
                if (step == 10)
                {
                    Assert.InRange(Math.Abs(magnitude - impulse.PeakTiltRadians), 0, 0.0000001);
                }
                previous = magnitude;
            }
            Assert.InRange(ReadTilt(MeteorMotion.Sample(impulse.StartSeconds + impulse.AngularDurationSeconds + 0.01)).Length(), 0, 0.0000001f);
        }
    }

    [Fact]
    public void SpinAndNegativeHistoryRemainContinuousAcrossTheirWrapBoundaries()
    {
        foreach (double boundary in new[] { 0d, 40, 60, 120, 450, MeteorMotion.ImpulsePeriodSeconds })
        {
            MeteorMotionSample before = MeteorMotion.Sample(boundary - 0.000001);
            MeteorMotionSample after = MeteorMotion.Sample(boundary + 0.000001);
            Assert.InRange(Vector3.Distance(before.AxisX, after.AxisX), 0, 0.000002f);
            Assert.InRange(Vector3.Distance(before.AxisY, after.AxisY), 0, 0.000002f);
            Assert.InRange(Vector3.Distance(before.AxisZ, after.AxisZ), 0, 0.000002f);
            Assert.InRange(Vector2.Distance(before.ScreenOffset, after.ScreenOffset), 0, 0.000002f);
        }
    }

    [Fact]
    public void OffsetsStayWithinAsymmetricFlightBoundsForTenMinutes()
    {
        for (int frame = 0; frame <= 600 * 60; frame++)
        {
            Vector2 offset = MeteorMotion.SampleScreenOffset(frame / 60d);
            AssertFlightBounds(offset);
        }
    }

    [Fact]
    public void SamplingOrderAndPresentationFrameRateDoNotChangeMotion()
    {
        MeteorMotionSample[] atThirtyFrames = new MeteorMotionSample[3601];
        for (int frame = 0; frame < atThirtyFrames.Length; frame++)
        {
            atThirtyFrames[frame] = MeteorMotion.Sample(frame / 30d);
        }
        for (int frame = 7200; frame >= 0; frame--)
        {
            MeteorMotionSample sample = MeteorMotion.Sample(frame / 60d);
            if (frame % 2 == 0)
            {
                Assert.Equal(atThirtyFrames[frame / 2], sample);
            }
        }
        Assert.Equal(atThirtyFrames[3600], MeteorMotion.Sample(120));
    }

    [Theory]
    [InlineData(-0.25)]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    [InlineData(449.999999)]
    [InlineData(450)]
    [InlineData(450.000001)]
    [InlineData(1234567890123)]
    [InlineData(-1234567890123)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void BasisIsOrthonormalAndTracksPointEightDegreeSpinWithBoundedImpulseTilt(double seconds)
    {
        MeteorMotionSample motion = MeteorMotion.Sample(seconds);
        AssertFlightBounds(motion.ScreenOffset);
        Assert.InRange(Math.Abs(motion.AxisX.LengthSquared() - 1), 0, 0.000002f);
        Assert.InRange(Math.Abs(motion.AxisY.LengthSquared() - 1), 0, 0.000002f);
        Assert.InRange(Math.Abs(motion.AxisZ.LengthSquared() - 1), 0, 0.000002f);
        Assert.InRange(Math.Abs(Vector3.Dot(motion.AxisX, motion.AxisY)), 0, 0.000002f);
        Assert.InRange(Math.Abs(Vector3.Dot(motion.AxisX, motion.AxisZ)), 0, 0.000002f);
        Assert.InRange(Math.Abs(Vector3.Dot(motion.AxisY, motion.AxisZ)), 0, 0.000002f);
        Assert.InRange(Vector3.Distance(Vector3.Cross(motion.AxisX, motion.AxisY), motion.AxisZ), 0, 0.000002f);
        double radians = seconds % 450 * Math.PI / 225;
        Vector3 expectedY = new(0, (float)Math.Cos(radians), (float)Math.Sin(radians));
        Vector3 expectedZ = new(0, -(float)Math.Sin(radians), (float)Math.Cos(radians));
        Assert.InRange(Vector3.Distance(Vector3.UnitX, motion.AxisX), 0, 0.005237f);
        Assert.InRange(Vector3.Distance(expectedY, motion.AxisY), 0, 0.005237f);
        Assert.InRange(Vector3.Distance(expectedZ, motion.AxisZ), 0, 0.005237f);
        Assert.InRange(Math.Abs(Math.Atan2(motion.AxisX.Y, motion.AxisX.X)), 0, Math.PI / 600 + 0.000001);
        Assert.InRange(Math.Abs(Math.Asin(Math.Clamp(-motion.AxisX.Z, -1, 1))), 0, Math.PI / 600 + 0.000001);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.125)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(120)]
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
        Assert.Throws<ArgumentOutOfRangeException>(() => MeteorMotion.SampleFlameIntensity(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeteorMotion.FillTailScreenOffsets(seconds, new Vector2[9]));
    }

    [Fact]
    public void TailRejectsAnIncompleteSampleBuffer()
    {
        Assert.Throws<ArgumentException>(() => MeteorMotion.FillTailScreenOffsets(0, new Vector2[8]));
    }

    private static Vector2 ReadTilt(MeteorMotionSample sample)
    {
        return new((float)Math.Asin(Math.Clamp(-sample.AxisX.Z, -1, 1)), (float)Math.Atan2(sample.AxisX.Y, sample.AxisX.X));
    }

    private static Vector2 ToFlight(Vector2 screen)
    {
        return new(0.94f * screen.X - 0.341f * screen.Y, 0.341f * screen.X + 0.94f * screen.Y);
    }

    private static float FlightPosition(double seconds)
    {
        return ToFlight(MeteorMotion.SampleScreenOffset(seconds)).X;
    }

    private static float FlightVelocity(double seconds)
    {
        return (FlightPosition(seconds + 0.1) - FlightPosition(seconds - 0.1)) / 0.2f;
    }

    private static void AssertFlightBounds(Vector2 screen)
    {
        Vector2 flight = ToFlight(screen);
        Assert.InRange(flight.X, -0.180001f * 0.24f, 0.300001f * 0.24f);
        Assert.InRange(Math.Abs(flight.Y), 0, 0.00000002f);
    }
}
