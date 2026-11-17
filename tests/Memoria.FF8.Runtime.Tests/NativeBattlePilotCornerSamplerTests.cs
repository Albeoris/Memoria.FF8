using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePilotCornerSamplerTests
{
    [Theory]
    [InlineData(-0.25f, false)]
    [InlineData(0f, false)]
    [InlineData(0.5f, false)]
    [InlineData(1f, true)]
    [InlineData(1.25f, true)]
    public void Endpoints_take_precedence_over_incompatible_generation_continuity_and_corner_count(float alpha, bool useLater)
    {
        NativeBattlePilotVisualGeometry earlier = Geometry(5);
        NativeBattlePilotVisualGeometry later = Geometry(51) with
        {
            Identity = earlier.Identity with { ResourceGeneration = 4 },
            ScreenSpace = true,
            Continuous = false,
            CornerCount = 3
        };

        NativeBattlePilotCornerSampler sampler = NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, later, alpha);
        NativeBattlePilotVisualGeometry expected = useLater ? later : earlier;
        Assert.Same(expected, NativeBattlePilotVisualGeometry.Sample(earlier, later, alpha));
        for (int corner = 0; corner < expected.CornerCount; corner++)
        {
            Assert.Equal(expected.Corner(corner), sampler.SampleCorner(corner));
        }
        if (useLater)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => sampler.SampleCorner(3));
        }
    }

    [Fact]
    public void Whole_geometry_keeps_triangle_corner_copy_when_screen_space_differs()
    {
        NativeBattlePilotVisualGeometry earlier = Geometry(0) with { CornerCount = 3 };
        NativeBattlePilotVisualGeometry later = Geometry(100) with { CornerCount = 3, ScreenSpace = true };
        NativeBattlePilotVisualGeometry sampled = NativeBattlePilotVisualGeometry.Sample(earlier, later, 0.5f);

        Assert.NotSame(earlier, sampled);
        Assert.Equal(earlier with { D = default }, sampled);
        Assert.Equal(earlier.A, NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, later, 0.5f).SampleCorner(0));
    }

    [Fact]
    public void Raw_and_authored_endpoints_use_identical_corner_interpolation()
    {
        NativeBattlePilotVisualGeometry earlier = Geometry(-7);
        NativeBattlePilotVisualGeometry later = Geometry(12);
        NativeBattlePilotGeometryReference rawEarly = Compact(earlier);
        NativeBattlePilotGeometryReference rawLate = Compact(later);
        const float alpha = 0.346f;

        NativeBattlePilotCornerSampler compact = NativeBattlePilotVisualGeometry.PrepareCornerSampling(rawEarly, rawLate, alpha);
        NativeBattlePilotCornerSampler mixed = NativeBattlePilotVisualGeometry.PrepareCornerSampling(rawEarly, later, alpha);
        NativeBattlePilotVisualGeometry all = NativeBattlePilotVisualGeometry.Sample(earlier, later, alpha);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 expected = Vector3.Lerp(earlier.Corner(corner), later.Corner(corner), alpha);
            AssertBits(expected, compact.SampleCorner(corner));
            AssertBits(expected, mixed.SampleCorner(corner));
            AssertBits(expected, all.Corner(corner));
            AssertBits(expected, NativeBattlePilotVisualGeometry.SampleCorner(earlier, later, corner, alpha));
            AssertBits(expected, NativeBattlePilotVisualGeometry.SampleCorner(rawEarly, rawLate, corner, alpha));
        }
    }

    [Fact]
    public void Sprite_interpolation_keeps_earlier_local_corners_and_original_operation_order()
    {
        NativeBattlePilotSpriteGeometry earlySprite = new(new(-4, -3, 1), new(2, -3, 2), new(-4, 5, 3), new(2, 5, 4), new(4, 6, 8), new(1, 2, 4), new(4, 1, 2), new(2, 4, 1));
        NativeBattlePilotSpriteGeometry lateSprite = earlySprite with { LocalA = new(50, 50, 50), Origin = new(7, 11, 13), AxisX = new(0, -2, 4), AxisY = new(-4, 1, 0), AxisZ = new(3, 0, -1) };
        NativeBattlePilotVisualGeometry earlier = Geometry(1) with { Sprite = earlySprite };
        NativeBattlePilotVisualGeometry later = Geometry(12) with { Sprite = lateSprite };
        const float alpha = 0.343f;

        NativeBattlePilotCornerSampler sampler = NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, later, alpha);
        NativeBattlePilotCornerSampler missingSprite = NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, later with { Sprite = null }, alpha);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 local = earlySprite.LocalCorner(corner);
            Vector3 expected = Vector3.Lerp(earlySprite.Origin, lateSprite.Origin, alpha)
                + local.X * Vector3.Lerp(earlySprite.AxisX, lateSprite.AxisX, alpha)
                + local.Y * Vector3.Lerp(earlySprite.AxisY, lateSprite.AxisY, alpha)
                + local.Z * Vector3.Lerp(earlySprite.AxisZ, lateSprite.AxisZ, alpha);
            AssertBits(expected, sampler.SampleCorner(corner));
            AssertBits(Vector3.Lerp(earlier.Corner(corner), later.Corner(corner), alpha), missingSprite.SampleCorner(corner));
        }
        Assert.Equal(later.A, NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, later, 1).SampleCorner(0));
    }

    [Fact]
    public void Preparing_and_reading_model_corners_allocates_no_managed_storage()
    {
        NativeBattlePilotGeometryReference earlier = Compact(Geometry(1));
        NativeBattlePilotGeometryReference later = Compact(Geometry(12));
        _ = ReadMany(earlier, later);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Vector3 total = ReadMany(earlier, later);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.NotEqual(Vector3.Zero, total);
        Assert.Equal(0L, allocated);
    }

    private static Vector3 ReadMany(NativeBattlePilotGeometryReference earlier, NativeBattlePilotGeometryReference later)
    {
        Vector3 total = default;
        for (int index = 0; index < 1024; index++)
        {
            NativeBattlePilotCornerSampler sampler = NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, later, index / 1024f);
            for (int corner = 0; corner < 4; corner++)
            {
                total += sampler.SampleCorner(corner);
            }
        }
        return total;
    }

    private static NativeBattlePilotVisualGeometry Geometry(float x)
    {
        return new(new(0, 0x502D40, 11, 3, 4), new(x, 0, 30), new(x + 8, 0, 50), new(x, 8, 70), new(x + 8, 8, 90), 4);
    }

    private static NativeBattlePilotGeometryReference Compact(NativeBattlePilotVisualGeometry geometry)
    {
        NativeBattleModelPrimitiveBatch.Builder batch = new();
        int emission = batch.BeginEmission(new(null, null, null, geometry.Identity, geometry.Continuous, geometry.ScreenSpace));
        NativeBattlePilotGeometryReference pilot = batch.AddPilot(new(geometry.A, geometry.B, geometry.C, geometry.D, geometry.Identity.Primitive, emission, geometry.CornerCount));
        batch.Freeze();
        return pilot;
    }

    private static void AssertBits(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.X), BitConverter.SingleToInt32Bits(actual.X));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.Y), BitConverter.SingleToInt32Bits(actual.Y));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.Z), BitConverter.SingleToInt32Bits(actual.Z));
    }
}
