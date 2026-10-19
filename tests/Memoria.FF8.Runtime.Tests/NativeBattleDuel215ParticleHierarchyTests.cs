namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215ParticleHierarchyTests
{
    [Theory]
    [InlineData(1024, 2048, -2048)]
    [InlineData(-1024, -2048, 2048)]
    public void ChildBasisUsesNegatedSignedAngleAndScaledYaw(short angle, short right, short left)
    {
        NativeBattleActionMatrix matrix = NativeBattleDuel215Controller.ParticleBasis6ED1E0(angle, 2048);
        Assert.Equal(new short[] { 0, 0, right, 0, 2048, 0, left, 0, 0 }, matrix.Rotation);
        Assert.Equal((0, 0, 0), (matrix.X, matrix.Y, matrix.Z));
    }
}
