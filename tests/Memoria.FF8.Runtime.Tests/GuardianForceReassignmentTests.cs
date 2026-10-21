using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GuardianForceReassignmentTests
{
    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(3, 7, false)]
    [InlineData(0, 1, true)]
    [InlineData(3, 7, true)]
    public void TransferRevalidatesThePreviousOwnerAndPreservesStockLearningAndUnrelatedCharacters(int owner, int destination, bool retainOtherGf)
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        for (int row = 0; row < source.Records.Length; row++)
        {
            source.Records[row] = source.Records[row] with { Junction = source.Records[row].Junction with { GuardianForces = 0 } };
        }
        source.GuardianForces[0] = source.GuardianForces[0] with { LearnedBank = new NativeGuardianForceLearnedBank((1UL << 1) | (1UL << 18) | (1UL << 21) | (1UL << 39), 0), Learning = 5, AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 7) };
        source.GuardianForces[1] = source.GuardianForces[1] with { LearnedBank = new NativeGuardianForceLearnedBank((1UL << 2) | (1UL << 22) | (1UL << 40) | (1UL << 41), 0) };
        source.GuardianForces[2] = source.GuardianForces[2] with { LearnedBank = new NativeGuardianForceLearnedBank((1UL << 2) | (1UL << 23) | (1UL << 42), 0) };
        source = MagicSystemFixture.Stock(source, owner, 1, 100);
        source = MagicSystemFixture.Stock(source, owner, 2, 100);
        source = MagicSystemFixture.Stock(source, destination, 2, 100);
        byte[] ownerMagic = new byte[20];
        ownerMagic[0] = 1;
        ownerMagic[1] = 2;
        byte[] destinationMagic = new byte[20];
        destinationMagic[1] = 2;
        NativeCharacterVital previous = source.Records[owner];
        source.Records[owner] = previous with { HitPoints = new NativeHpWord(90, 90), Status = 4, Junction = new NativeCharacterJunction(retainOtherGf ? (ushort)3 : (ushort)1, 21u | (22u << 8), 39u | (40u << 8) | (41u << 16), ownerMagic) };
        previous = source.Records[destination];
        source.Records[destination] = previous with { HitPoints = new NativeHpWord(1, 1), Status = 4, Junction = new NativeCharacterJunction(4, 23, 42, destinationMagic) };
        string original = JsonSerializer.Serialize(source);

        NativeCharacterVitals result = source.ReassignGuardianForceJunction(destination, 0, owner);

        Assert.Equal(retainOtherGf ? (ushort)2 : (ushort)0, result.Records[owner].Junction.GuardianForces);
        Assert.Equal((ushort)5, result.Records[destination].Junction.GuardianForces);
        Assert.Equal(retainOtherGf ? 22u << 8 : 0u, result.Records[owner].Junction.Commands);
        Assert.Equal(retainOtherGf ? 40u << 8 : 0u, result.Records[owner].Junction.Abilities);
        Assert.Equal(0, result.Records[owner].Junction.MagicJunctions[0]);
        Assert.Equal(retainOtherGf ? (byte)2 : (byte)0, result.Records[owner].Junction.MagicJunctions[1]);
        Assert.Equal(new NativeHpWord(10, 10), result.Records[owner].HitPoints);
        Assert.Equal((ushort)4, result.Records[owner].Status);
        Assert.Equal(source.Records[destination] with { Junction = source.Records[destination].Junction with { GuardianForces = 5 } }, result.Records[destination]);
        Assert.Equal(source.Records[owner].MagicInventory, result.Records[owner].MagicInventory);
        Assert.Equal(source.Records.Where((_, row) => row != owner && row != destination), result.Records.Where((_, row) => row != owner && row != destination));
        Assert.Equal(source.CombatParty, result.CombatParty);
        Assert.Equal(source.GuardianForces[0] with { MaximumHitPoints = result.GuardianForces[0].MaximumHitPoints }, result.GuardianForces[0]);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(result))!;
        restored.Validate();
        Assert.Equal(result.Records, restored.Records);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("duplicate")]
    [InlineData("unowned")]
    [InlineData("unacquired")]
    [InlineData("same-owner")]
    public void TransferRejectsInvalidOwnershipWithoutMutatingTheSource(string scenario)
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        int expectedOwner = scenario == "stale" ? 2 : 0;
        int destination = scenario == "same-owner" ? 0 : 1;
        if (scenario == "duplicate")
        {
            source.Records[2] = source.Records[2] with { Junction = source.Records[2].Junction with { GuardianForces = 5 } };
        }
        if (scenario == "unowned")
        {
            source.Records[0] = source.Records[0] with { Junction = source.Records[0].Junction with { GuardianForces = 0 } };
        }
        if (scenario == "unacquired")
        {
            source.GuardianForces[0] = source.GuardianForces[0] with { Flags = 0 };
        }
        string original = JsonSerializer.Serialize(source);
        Assert.Throws<InvalidOperationException>(() => source.ReassignGuardianForceJunction(destination, 0, expectedOwner));
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(8, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 16, 0)]
    [InlineData(1, 0, -1)]
    [InlineData(1, 0, 8)]
    public void TransferRejectsOutOfRangeIdentities(int destination, int guardianForce, int owner)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicSystemFixture.Create().ReassignGuardianForceJunction(destination, guardianForce, owner));
    }
}
