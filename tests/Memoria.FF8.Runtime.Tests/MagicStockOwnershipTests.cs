using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MagicStockOwnershipTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(7, 8)]
    [InlineData(255, 0)]
    public void NativePersonalInsertionIncrementsTheFirstZeroIdRetainedByteWithoutReusingOtherZeroCounts(byte retained, byte expected)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[3].MagicInventory[0] = new(1, 0);
        vitals.Records[3].MagicInventory[1] = new(0, retained);
        vitals.Records[3].MagicInventory[2] = new(0, 42);
        string before = JsonSerializer.Serialize(vitals);

        NativeCharacterVitals next = vitals.AddMagicUnit(3, 6, out int result);

        Assert.Equal(0, result);
        Assert.Equal(new NativeMagicStock(1, 0), next.Records[3].MagicInventory[0]);
        Assert.Equal(new NativeMagicStock(6, expected), next.Records[3].MagicInventory[1]);
        Assert.Equal(new NativeMagicStock(0, 42), next.Records[3].MagicInventory[2]);
        Assert.Equal(before, JsonSerializer.Serialize(vitals));
        Assert.NotSame(vitals.Records[3].MagicInventory, next.Records[3].MagicInventory);
        next.Validate();
    }

    [Fact]
    public void NativePersonalInsertionUsesTheFirstMatchingIdIncludingZeroCountAndItsOwnCapacity()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[0].MagicInventory[0] = new(0, 255);
        vitals.Records[0].MagicInventory[1] = new(6, 0);
        vitals.Records[0].MagicInventory[2] = new(6, 99);
        NativeCharacterVitals next = vitals.AddMagicUnit(0, 6, out int result);
        Assert.Equal(0, result);
        Assert.Equal(new NativeMagicStock(0, 255), next.Records[0].MagicInventory[0]);
        Assert.Equal(new NativeMagicStock(6, 1), next.Records[0].MagicInventory[1]);
        Assert.Equal(new NativeMagicStock(6, 99), next.Records[0].MagicInventory[2]);
        Assert.Equal(new NativeMagicStock(6, 0), vitals.Records[0].MagicInventory[1]);
        next.Records[0].MagicInventory[1] = new(6, 100);
        Assert.Same(next, next.AddMagicUnit(0, 6, out result));
        Assert.Equal(1, result);
    }

    [Fact]
    public void NativePersonalInsertionRejectsAllOccupiedIdsEvenWhenTheirCountsAreZero()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        for (int slot = 0; slot < 32; slot++)
        {
            vitals.Records[0].MagicInventory[slot] = new((byte)(slot + 1), 0);
        }
        Assert.Same(vitals, vitals.AddMagicUnit(0, 40, out int result));
        Assert.Equal(2, result);
    }

    [Fact]
    public void NativeInsertionKeepsSharedStockOnTheApprovedSharedBankPolicy()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Modes(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 6, 10), shared: true);
        string before = JsonSerializer.Serialize(vitals);
        NativeCharacterVitals next = vitals.AddMagicUnit(3, 6, out int result);
        Assert.Equal(0, result);
        Assert.Equal(11, next.GetMagicCount(3, 6));
        Assert.Equal(11, next.GetMagicCount(0, 6));
        Assert.Equal(0, next.Records[3].MagicInventory.Sum(stock => stock.Count));
        Assert.Equal(before, JsonSerializer.Serialize(vitals));
        next = next.ChangeMagicStock(0, 6, 89, MagicOperationReason.ScriptReward, out _);
        Assert.Same(next, next.AddMagicUnit(3, 6, out result));
        Assert.Equal(1, result);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, -1, 2)]
    [InlineData(false, 57, 2)]
    [InlineData(true, 0, 0)]
    [InlineData(true, -1, 2)]
    [InlineData(true, 57, 2)]
    public void NativeInsertionDoesNotExpandTheSupportedMagicDomain(bool shared, int magic, int expected)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Modes(MagicSystemFixture.Create(), shared: shared);
        Assert.Same(vitals, vitals.AddMagicUnit(0, magic, out int result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NativeJunctionLookupIncludesTheTwentiethSavedByte()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[0].Junction.MagicJunctions[19] = 6;
        Assert.True(NativeCharacterVitals.IsMagicJunctioned(vitals.Records[0], 6));
        Assert.False(NativeCharacterVitals.IsMagicJunctioned(vitals.Records[0], 0));
        Assert.False(NativeCharacterVitals.IsMagicJunctioned(vitals.Records[0], 5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void PersonalDebitReconcilesOnlyItsOwnRecord(int amount)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 10);
        vitals = MagicSystemFixture.Stock(vitals, 3, 1, 20);
        vitals.Records[3] = vitals.Records[3] with { HitPoints = new(65535, 65535) };
        vitals.Records[3].Junction.MagicJunctions[0] = 1;
        vitals.Records[3].Junction.MagicJunctions[1] = 2;
        string unrelated = JsonSerializer.Serialize(vitals.Records[3]);
        string before = JsonSerializer.Serialize(vitals);
        NativeCharacterVitals next = vitals.ChangeMagicStock(0, 1, -amount, MagicOperationReason.RefinementInput, out MagicStockMutation mutation);
        Assert.Equal(amount, mutation.Accepted);
        Assert.Equal(new[] { 0 }, mutation.AffectedRecords);
        Assert.DoesNotContain(mutation.RemovedJunctions, change => change.RecordId == 3);
        Assert.Equal(unrelated, JsonSerializer.Serialize(next.Records[3]));
        Assert.Equal(before, JsonSerializer.Serialize(vitals));
    }
}
