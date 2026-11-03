using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterTerminalResultTests
{
    [Fact]
    public void NativeEncounterTerminalCanReturnTheDefeatedPartyWithoutRevivingIt()
    {
        BattleResult result = Result();
        Assert.Throws<InvalidDataException>(() => result.Validate());
        result = result with { EncounterTerminal = new(EncounterBindingResolver.Disc1Edea, "generation", "original") };
        result.Validate();
        BattleResult restored = JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(result))!;
        restored.Validate();
        Assert.Equal(result.EncounterTerminal, restored.EncounterTerminal);
        Assert.All(restored.Party, member =>
        {
            Assert.Equal((ushort)0, member.HitPointsAfter);
            Assert.Equal((ushort)1, member.StatusAfter);
        });
    }

    [Theory]
    [InlineData(195, 4)]
    [InlineData(136, 1)]
    public void OtherEncounterOrNativeResultCannotUseTheTerminalReceipt(int formation, byte nativeResult)
    {
        BattleResult result = Result();
        result = result with { Encounter = result.Encounter with { Formation = formation }, NativeResult = nativeResult, EncounterTerminal = new(EncounterBindingResolver.Disc1Edea, "generation", "original") };
        Assert.Throws<InvalidDataException>(() => result.Validate());
    }

    private static BattleResult Result()
    {
        string hash = new('A', 64);
        BattleEncounterOccurrence encounter = new("occurrence", 136, hash, SceneKind.Field, 768, 1, hash, new(768, 0, 4, 93, 18, hash));
        return new(encounter, BattleOutcome.Victory, [new(0, 0, 1000, 0, 0, 1, [])], [], [], [], false, BattleRewardPolicy.NativeCombat, []) { NativeResult = 4 };
    }
}
