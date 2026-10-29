using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleInstalledSnapshotRoundTripTests
{
    [Theory]
    [InlineData("battle-flare-task-bank-snapshot")]
    [InlineData("battle-diablos-task-bank-snapshot")]
    [InlineData("battle-carbuncle-task-bank-snapshot")]
    public void Installed_effect_snapshot_restores_without_changing_physical_memory(string fixture)
    {
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(TestResources.FixturePath(fixture)))!;
        JsonElement expected = JsonSerializer.SerializeToElement(snapshot);
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(expected.GetRawText())!);
        BattleSnapshot firstRestored = first.Capture();
        BattleSnapshot secondRestored = second.Capture();
        Assert.True(JsonElement.DeepEquals(expected.GetProperty("NativeEffectMemory").GetProperty("Arena"), JsonSerializer.SerializeToElement(firstRestored.NativeEffectMemory!.Arena)), "Restoring a task bank must preserve the captured physical arena.");
        Assert.True(JsonElement.DeepEquals(expected, JsonSerializer.SerializeToElement(firstRestored)), "Restoring an installed effect must preserve the complete captured battle.");
        Assert.True(JsonElement.DeepEquals(expected, JsonSerializer.SerializeToElement(secondRestored)), "A second restoration must use independent owners and preserve the captured battle.");
        Assert.True(JsonElement.DeepEquals(expected, JsonSerializer.SerializeToElement(snapshot)), "Restoration must not mutate its source snapshot.");
    }
}
