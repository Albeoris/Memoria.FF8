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
    public void Authored_hand_envelope_admits_a_crossing_weapon_surface_and_rejects_a_released_one()
    {
        Assert.True(NativeBattleWeaponContactGeometry.IntersectsHandEnvelope(-Vector3.One, Vector3.One, new(-4, 0, 0), new(4, 0, 0), new(0, 4, 0)));
        Assert.False(NativeBattleWeaponContactGeometry.IntersectsHandEnvelope(-Vector3.One, Vector3.One, new(-4, 0, 2), new(4, 0, 2), new(0, 4, 2)));
        Assert.Throws<InvalidDataException>(() => NativeBattleWeaponContactGeometry.IntersectsHandEnvelope(Vector3.One, -Vector3.One, Vector3.Zero, Vector3.UnitX, Vector3.UnitY));
    }

    [Fact]
    public void Coupled_contact_moves_the_primary_only_when_the_secondary_requires_shared_reach()
    {
        Vector3 preferred = new(3, 0, 0);
        Vector3 gap = new(0, 1, 0);
        Vector3 secondaryShoulder = new(-2, 1, 0);
        Vector3 result = NativeBattleCoupledHandContact.SolvePrimary(preferred, Vector3.Zero, 2, 2, secondaryShoulder, 2, 2, gap);
        Assert.InRange(Vector3.Distance(result, preferred), 0.9999f, 1.0001f);
        Assert.InRange(Vector3.Distance(result, Vector3.Zero), 0, 4);
        Assert.InRange(Vector3.Distance(result + gap, secondaryShoulder), 0, 4);
        Vector3 admitted = new(1, 0, 0);
        Assert.Equal(admitted, NativeBattleCoupledHandContact.SolvePrimary(admitted, Vector3.Zero, 2, 2, secondaryShoulder, 2, 2, gap));
    }

    [Fact]
    public void Coupled_contact_uses_the_nearest_shared_sphere_intersection()
    {
        Vector3 result = NativeBattleCoupledHandContact.SolvePrimary(new(0, 6, 0), new(-2, 0, 0), 1.5f, 1.5f, new(2, 0, 0), 1.5f, 1.5f, Vector3.Zero);
        Assert.InRange(MathF.Abs(result.X), 0, 0.0001f);
        Assert.InRange(MathF.Abs(result.Y - MathF.Sqrt(5)), 0, 0.0001f);
        Assert.InRange(MathF.Abs(result.Z), 0, 0.0001f);
    }

    [Fact]
    public void Coupled_contact_reports_incompatible_shells_and_rejects_nonfinite_inputs()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCoupledHandContact.SolvePrimary(Vector3.Zero, Vector3.Zero, 1, 1, new(10, 0, 0), 1, 1, Vector3.Zero));
        Assert.Throws<InvalidDataException>(() => NativeBattleCoupledHandContact.SolvePrimary(Vector3.Zero, Vector3.Zero, 1, 1, Vector3.Zero, 1, 1, new(float.NaN, 0, 0)));
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
