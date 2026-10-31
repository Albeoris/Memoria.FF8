using System.Text.Json;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class FreshEntryPairRunnerTests
{
    [Fact]
    public void SummaryKeepsIndependentColdPairProtocolAndPinsDistinctFromDefaultRepeat()
    {
        PilotCaseSummary ordinary = PilotRunner.Summarize("case", "PASS", [], 2, 100, 90, Path.GetTempPath(), true);
        Assert.Equal("same-battle-repeat", ordinary.ExecutionProtocol);
        Assert.Null(ordinary.PairPinsPath);
        string path = Path.Combine(Path.GetTempPath(), "fresh-entry-pair-pins.json");
        PilotCaseSummary paired = ordinary with { ExecutionProtocol = "fresh-entry-pair", PairPinsPath = path };
        PilotCaseSummary restored = JsonSerializer.Deserialize<PilotCaseSummary>(JsonSerializer.Serialize(paired, RecordJson.Options), RecordJson.Options)!;
        Assert.Equal("fresh-entry-pair", restored.ExecutionProtocol);
        Assert.Equal(path, restored.PairPinsPath);
        Assert.Equal(2, restored.Executions);
    }

    [Fact]
    public void MissingExplicitExecutionStackCannotFallBackToAnotherProfile()
    {
        Assert.Throws<InvalidDataException>(() => PilotRunner.ResolveFreshEntryExecutionStack(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}
