namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel212BodyTests
{
    [Fact]
    public void ZeroRotationKeepsOriginalModelAndCameraTranslation()
    {
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = 120, Y = -500, Z = 1800 };
        NativeBattleActionMatrix view = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = -10, Y = 30, Z = 250 };
        NativeBattleActionMatrix result = NativeBattleDuel212Body.Matrix6A2F60(world, view, new(0, 0, 0), new(50, -100, 200));
        Assert.Equal(110, result.X);
        Assert.Equal(-470, result.Y);
        Assert.Equal(2050, result.Z);
    }

    [Fact]
    public void HalfTurnUsesTheCurrentPivotBeforeModelTranslation()
    {
        NativeBattleActionMatrix identity = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        NativeBattleActionMatrix result = NativeBattleDuel212Body.Matrix6A2F60(identity with { X = 100, Z = 1000 }, identity, new(0, 2048, 0), new(50, 0, -200));
        Assert.Equal(200, result.X);
        Assert.Equal(600, result.Z);
        Assert.Equal((short)-4096, result.Rotation[0]);
        Assert.Equal((short)-4096, result.Rotation[8]);
    }
}
