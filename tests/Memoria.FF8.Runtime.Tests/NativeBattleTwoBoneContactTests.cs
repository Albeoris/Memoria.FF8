using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTwoBoneContactTests
{
    [Theory]
    [InlineData(100, 80, 120)]
    [InlineData(200, 160, 240)]
    [InlineData(100, 80, 20)]
    [InlineData(100, 80, 180)]
    public void Analytic_contact_preserves_both_limb_lengths_and_the_measured_target(float upper, float lower, float distance)
    {
        Vector3 shoulder = new(10, 20, 30);
        Vector3 elbow = shoulder + Vector3.UnitX * upper;
        Vector3 wrist = elbow + Vector3.UnitY * lower;
        Vector3 desired = shoulder + Vector3.UnitX * distance;
        NativeBattleTwoBoneContactSolution result = NativeBattleTwoBoneContact.Solve(shoulder, elbow, wrist, desired, Vector3.UnitY);
        Assert.InRange(MathF.Abs(Vector3.Distance(shoulder, result.Elbow) - upper), 0, 0.0001f);
        Assert.InRange(MathF.Abs(Vector3.Distance(result.Elbow, result.Wrist) - lower), 0, 0.0001f);
        Assert.Equal(desired, result.Wrist);
        Assert.True(result.Elbow.Y >= shoulder.Y);
    }

    [Fact]
    public void Unreachable_contact_reports_the_actual_interval_without_clamping_the_target()
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => NativeBattleTwoBoneContact.Solve(Vector3.Zero, Vector3.UnitX * 2, Vector3.UnitX * 4, Vector3.UnitX * 5, Vector3.UnitY));
        Assert.Contains("distance 5", exception.Message);
        Assert.Contains("[0, 4]", exception.Message);
        Assert.Throws<InvalidDataException>(() => NativeBattleTwoBoneContact.Solve(Vector3.Zero, Vector3.One, Vector3.One * 2, new(float.NaN, 0, 0), Vector3.UnitY));
    }
}
