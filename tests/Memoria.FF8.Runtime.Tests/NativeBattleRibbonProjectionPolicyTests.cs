using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRibbonProjectionPolicyTests
{
    [Fact]
    public void Native_policy_retains_missing_scratch_failure_and_explicit_signed_direction()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleTrailOutline.Expand(Points(0, 0, 10, 20), Distance(), 1024));
        NativeBattleTrailOutline.Point[] outline = Points(0, 0, 10, 20);
        NativeBattleTrailOutline.Expand(outline, Distance(), 1024, 0xFFFF0001);
        Assert.Equal(-1, outline[0].QuarterDepth);
        Assert.True(outline[1].ScreenWritten);
    }

    [Fact]
    public void Independent_runs_ignore_external_scratch_and_restart_after_degenerate_edges()
    {
        NativeBattleTrailOutline.Point[] outline = Points(0, 0, 10, 10, 20, 30);
        NativeBattleTrailOutline.Point[] other = Points(0, 0, 10, 10, 20, 30);
        NativeBattleTrailOutline.Expand(outline, Distance(), 1024, projectionPolicy: NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns);
        NativeBattleTrailOutline.Expand(other, Distance(), 1024, 0xFFFF0001, NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns);
        Assert.Equal(new[] { false, true, false, true, true, false }, outline.Select(point => point.CanConnectToNext));
        Assert.Equal(outline.Select(point => (point.Left, point.Right)), other.Select(point => (point.Left, point.Right)));
        Assert.Equal(Screen(0), outline[0].Left);
        Assert.Equal(Screen(10), outline[2].Left);
        Assert.Equal(Screen(30), outline[5].Left);
        Assert.All(outline, point => Assert.True(point.ScreenWritten));
    }

    [Fact]
    public void Unclipped_nondegenerate_geometry_matches_the_native_outline()
    {
        NativeBattleTrailOutline.Point[] native = Points(0, 10, 20, 30);
        NativeBattleTrailOutline.Point[] host = Points(0, 10, 20, 30);
        NativeBattleTrailOutline.Expand(native, Distance(), 512);
        NativeBattleTrailOutline.Expand(host, Distance(), 512, projectionPolicy: NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns);
        Assert.Equal(native.Select(point => (point.Left, point.Right)), host.Select(point => (point.Left, point.Right)));
        Assert.All(host.Take(3), point => Assert.True(point.CanConnectToNext));
        Assert.False(host[3].CanConnectToNext);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Invalid_depth_breaks_connections_and_the_next_visible_run_has_its_own_direction(short depth)
    {
        NativeBattleTrailOutline.Point[] outline = Points(0, 10, 20, 30, 40);
        outline[1].QuarterDepth = depth;
        NativeBattleTrailOutline.Expand(outline, Distance(), 1024, projectionPolicy: NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns);
        NativeBattleTrailOutline.Point[] isolated = Points(20, 30, 40);
        NativeBattleTrailOutline.Expand(isolated, Distance(), 1024);
        Assert.False(outline[0].CanConnectToNext);
        Assert.False(outline[1].CanConnectToNext);
        Assert.False(outline[1].ScreenWritten);
        Assert.Equal(isolated.Select(point => (point.Left, point.Right)), outline.Skip(2).Select(point => (point.Left, point.Right)));
    }

    [Fact]
    public void Original_packet_producer_accepts_collapsed_host_geometry_without_a_stack_and_preserves_depth_observation()
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleCameraProjection projection = new(identity, 216, 160, 108);
        ImmutableArray<NativeBattleQuezacotlRibbonPoint> points = Enumerable.Repeat(new NativeBattleQuezacotlRibbonPoint(new(0, 0, 1000), 64, 128), 16).ToImmutableArray();
        List<ushort> observed = [];
        NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlRibbonPackets.Emit6C8C50(points, identity, projection, Distance(), observed.Add, projectionPolicy: NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns);
        Assert.Empty(batch.Attempts);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(Enumerable.Repeat((ushort)1000, 16), observed);
        ImmutableArray<NativeBattleQuezacotlRibbonPoint> restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattleQuezacotlRibbonPoint>>(JsonSerializer.Serialize(points));
        NativeBattleGfGeometryBatch continued = NativeBattleQuezacotlRibbonPackets.Emit6C8C50(restored, identity, projection, Distance(), projectionPolicy: NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns);
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(continued));
    }

    private static NativeBattleTrailOutline.Point[] Points(params int[] x)
    {
        return x.Select(value => new NativeBattleTrailOutline.Point(Screen(value), 250, () => 64)).ToArray();
    }

    private static uint Screen(int x)
    {
        return unchecked((ushort)x);
    }

    private static ImmutableArray<byte> Distance()
    {
        byte[] table = new byte[16384];
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                table[x + y * 128] = unchecked((byte)Math.Sqrt(x * x + y * y));
            }
        }
        return table.ToImmutableArray();
    }
}
