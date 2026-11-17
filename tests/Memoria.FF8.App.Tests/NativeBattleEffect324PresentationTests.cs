using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect324PresentationTests
{
    [Fact]
    public void Archived_effect324_report_records_warm_history_rejection_at_age17()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_OBSERVATION_DIRECTORY");
        Assert.SkipUnless(directory is not null, "Select the archived Effect324 warm-history rejection report.");
        using JsonDocument report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory!, "effect324-warm-history-rejection.json")));
        JsonElement recorded = report.RootElement;
        Assert.Equal(17, recorded.GetProperty("Age").GetInt32());
        Assert.Equal(0, recorded.GetProperty("NativePacketCount").GetInt32());
        Assert.True(recorded.GetProperty("ActualHistoryCommands").GetInt32() > 0);
        Assert.Equal(recorded.GetProperty("PriorNativeCallback").GetInt64() + 1, recorded.GetProperty("CurrentNativeCallback").GetInt64());
    }
}
