namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFunguarLaser277Tests
{
    [Fact]
    public void Beam_ease_keeps_unsigned_precharge_and_fade_boundaries()
    {
        Assert.Equal(((short)512, 0, 0), NativeBattleFunguarLaser277.Ease68433C(5, 256));
        Assert.Equal(((short)512, 0, 0), NativeBattleFunguarLaser277.Ease68433C(15, 256));
        Assert.Equal(((short)-512, 256, 0), NativeBattleFunguarLaser277.Ease68433C(35, 256));
        Assert.Equal(((short)-656, -32, 4608), NativeBattleFunguarLaser277.Ease68433C(44, 256));
    }

    [Fact]
    public void Floor_projection_uses_signed_division_then_word_narrowing()
    {
        Assert.Equal(new NativeBattleVector(150, 0, 250), NativeBattleFunguarLaser277.Floor68449D(new(100, -100, 200), new(200, 100, 300)));
        Assert.Equal(new NativeBattleVector(-1, 0, -1), NativeBattleFunguarLaser277.Floor68449D(new(-1, -1, -1), new(1, 2, 1)));
        Assert.Throws<DivideByZeroException>(() => NativeBattleFunguarLaser277.Floor68449D(new(0, 0, 0), new(1, 0, 1)));
    }

    [Fact]
    public void Basis_reduces_large_odd_differences_before_normalization_and_restores_length()
    {
        var actual = NativeBattleFunguarLaser277.Basis50CBA0(20001, -20001, 0);
        NativeBattleActionMatrix expected = NativeBattlePandemona291Restoration.Basis50CBA0(5001, -4998, 0, default);
        Assert.Equal(expected.Rotation.ToArray(), actual.Matrix.Rotation.ToArray());
        Assert.Equal(28280, actual.Length);
        Assert.Equal(0, NativeBattleFunguarLaser277.Basis50CBA0(0, 0, 0).Length);
    }
}
