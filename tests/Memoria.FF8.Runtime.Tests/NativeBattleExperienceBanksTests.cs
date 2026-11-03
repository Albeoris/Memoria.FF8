using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExperienceBanksTests
{
    [Theory]
    [InlineData(1, 40, 20)]
    [InlineData(0, 0, 20)]
    [InlineData(245, 0, 20)]
    [InlineData(246, 0, 20)]
    [InlineData(7, 0, 0)]
    [InlineData(29, 0, 0)]
    public void AccrualUsesActualCommandBranchesAndTheOriginalSharedAndPersonalOffsets(byte command, ushort personal, ushort shared)
    {
        NativeBattleExperienceBanks original = NativeBattleExperienceBanks.Initialize48D020();
        NativeBattleExperienceBanks accrued = original.Accrue494AF0(Input(command));
        Assert.All(original.Words, word => Assert.Equal((ushort)0, word));
        Assert.Equal(personal, accrued.ReadWord(6));
        Assert.Equal(shared, accrued.ReadWord(0));
        Assert.Equal((ushort)3, accrued.ReadWord(76));
        Assert.Equal((ushort)0, accrued.ReadWord(8));
    }

    [Fact]
    public void EnemyFinishingBlowAliasesTheSamePhysicalWordsRatherThanAnInventedEnemyBank()
    {
        NativeBattleExperienceBanks result = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(Input(1) with { ActorIndex = 3 });
        Assert.Equal((ushort)40, result.ReadWord(12));
        Assert.Equal((ushort)0, result.ReadWord(6));
    }

    [Fact]
    public void GuardianCreditWritesItsPersonalBankAndDefeatedGuardianDistributionClearsIt()
    {
        NativeBattleExperienceBanks banks = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(Input(254) with { GuardianForceId = 2 });
        Assert.Equal((ushort)40, banks.ReadWord(48));
        ushort[] hp = new ushort[16];
        hp[1] = 1;
        NativeBattleExperienceBanks result = banks.Distribute494D40(Party(new(true, 4, 6)), hp, [], false);
        Assert.Equal((ushort)10, result.ReadWord(14));
        Assert.Equal((ushort)0, result.ReadWord(16));
        Assert.Equal((ushort)0, result.ReadWord(48));
        Assert.Equal((ushort)3, result.ReadWord(78));
        Assert.Equal((ushort)0, result.ReadWord(76));
        Assert.Equal((ushort)0, result.ReadWord(6));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void GuardianEligibilityUsesKoClearOrPetrifyClear(ushort status, bool eligible)
    {
        ushort[] hp = new ushort[16];
        hp[0] = 1;
        NativeBattleExperienceBanks result = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(Input(1)).Distribute494D40(Party(new(true, status, 1)), hp, [], false);
        Assert.Equal(eligible ? (ushort)20 : (ushort)0, result.ReadWord(12));
        Assert.Equal(eligible ? (ushort)3 : (ushort)0, result.ReadWord(76));
        Assert.Equal(status == 0 ? (ushort)20 : (ushort)0, result.ReadWord(0));
    }

    [Fact]
    public void DistributionRetainsInactiveUnknownStatusWithoutGuessingPhysicalBytes()
    {
        NativeBattleExperienceBanks result = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(Input(1)).Distribute494D40(Party(new(true, 0, 0)), new ushort[16], [], false);
        Assert.Null(result.Words[1]);
        Assert.Equal((ushort)0, result.ReadWord(8));
        Assert.Throws<NotSupportedException>(() => result.ReadWord(2));
        Assert.Equal((ushort)20, result.ReadWord(0));
    }

    [Fact]
    public void SerializedAccrualContinuesIdenticallyAndNoExperienceClearsOnlyExperienceBanks()
    {
        NativeBattleExperienceBanks banks = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(Input(1));
        NativeBattleExperienceBanks restored = JsonSerializer.Deserialize<NativeBattleExperienceBanks>(JsonSerializer.Serialize(banks))!;
        NativeBattleExperienceSurvivor[] survivors = [new(0, 5, 10, 10, 100, 50), new(1, 500, 10, 10, 100, 0)];
        ushort[] hp = new ushort[16];
        hp[0] = 1;
        NativeBattleExperienceBanks next = restored.Distribute494D40(Party(new(true, 0, 1)), hp, survivors, false);
        Assert.Equal((ushort)30, next.ReadWord(0));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(banks.Distribute494D40(Party(new(true, 0, 1)), hp, survivors, false)));
        NativeBattleExperienceBanks cleared = restored.Distribute494D40(Party(new(true, 0, 1)), hp, survivors, true);
        Assert.All(cleared.Words.Take(38), word => Assert.Equal((ushort)0, word));
        Assert.Equal((ushort)3, cleared.ReadWord(76));
    }

    [Fact]
    public void AccumulationUsesWordWrappingBeforeThe60000Cap()
    {
        NativeBattleExperienceInput input = Input(1) with { PersonalBasis = 15000, SharedBasis = 15000 };
        NativeBattleExperienceBanks banks = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(input).Accrue494AF0(input);
        Assert.Equal(unchecked((ushort)120000), banks.ReadWord(6));
        Assert.Equal(unchecked((ushort)120000), banks.ReadWord(0));
    }

    [Fact]
    public void MissingOrInvalidPhysicalInputsAreRejected()
    {
        NativeBattleExperienceBanks banks = NativeBattleExperienceBanks.Initialize48D020();
        Assert.Throws<InvalidDataException>(() => new NativeBattleExperienceBanks(default).Validate());
        Assert.Throws<InvalidDataException>(() => banks.Accrue494AF0(Input(254)));
        Assert.Throws<InvalidDataException>(() => banks.Accrue494AF0(Input(1) with { ActorIndex = 7 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => banks.ReadWord(1));
        Assert.Throws<NotSupportedException>(() => banks.Distribute494D40(Party(new(true, null, 0)), new ushort[16], [], false));
    }

    private static NativeBattleExperienceInput Input(byte command) => new(0, command, null, 10, 10, 10, 10, 5, 100, 0, 3);

    private static NativeBattleExperiencePartySlot[] Party(NativeBattleExperiencePartySlot first) => [first, new(false, null, 0), new(false, null, 0)];
}
