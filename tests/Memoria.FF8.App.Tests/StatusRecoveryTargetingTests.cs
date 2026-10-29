using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class StatusRecoveryTargetingTests
{
    [Fact]
    public void RevivalOutranksEveryOtherRemovableNegativeConditionCombined()
    {
        Assert.True(StatusRecoveryTargeting.Score(1, 0) > StatusRecoveryTargeting.Score(0x7E, uint.MaxValue));
    }

    [Fact]
    public void BeneficialAndHpBookkeepingFlagsDoNotAttractARecoveryCursor()
    {
        Assert.Equal(0, StatusRecoveryTargeting.Score(0x380, 2 | 16 | 32 | 64 | 128 | 256 | 2048 | 8192 | 32768 | 131072 | 262144));
    }

    [Fact]
    public void SeverityAndCombinedRemovableConditionsBothMatter()
    {
        Assert.True(StatusRecoveryTargeting.Score(4, 0) > StatusRecoveryTargeting.Score(2, 0));
        Assert.True(StatusRecoveryTargeting.Score(2 | 16, 0) > StatusRecoveryTargeting.Score(2, 0));
        Assert.True(StatusRecoveryTargeting.Score(0, 8) > StatusRecoveryTargeting.Score(0, 4));
    }
}
