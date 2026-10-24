using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MagicStockOwnershipTests
{
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
