namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignTargetCenterTests
{
    [Theory]
    [InlineData(0, 4096, -3)]
    [InlineData(101, 2048, 99)]
    [InlineData(-101, 2048, -102)]
    public void Semantic_center_uses_current_body_transform_and_signed_midpoint(int y, int scale, short expected)
    {
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleForeignModelGeometry.Identity, 4096, scale, 4096) with { Y = y };
        NativeBattleForeignModelGeometry geometry = new(basis, new(new(0, 900, 0), new(0, 900, 0), 900, new(new(0, -900, 0), new(0, 900, 0))), [new(new(0, -10, 0), new(1, 3, 0), new(0, 0, 1), default!)]);
        Assert.Equal(expected, NativeBattleEffectModelHelpers.ReadVerticalCenter(geometry));
        Assert.Null(NativeBattleEffectModelHelpers.ReadVerticalCenter(null));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectModelHelpers.ReadVerticalCenter(geometry with { Faces = [] }));
    }
}
