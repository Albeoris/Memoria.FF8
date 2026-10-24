using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldPresentationSnapshotTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MissingWorldPresentationHistoryUsesColdDefaultsOnlyBeforeAnyCallbacks(int callbacks)
    {
        GameState state = GameSession.CreateColdProcess().State;
        state = state with { WorldProcess = state.WorldProcess!.AdvanceMainLoopCallbacks(callbacks) };
        JsonObject snapshot = JsonSerializer.SerializeToNode(state, Options)!.AsObject();
        JsonObject process = snapshot["WorldProcess"]!.AsObject();
        process.Remove("PreviousContactC76784");
        process.Remove("WaterStepEdges2045C7C");
        NativeWorldProcess restored = Deserialize(snapshot).WorldProcess!;
        Assert.Equal(callbacks == 0 ? -1 : (int?)null, restored.PreviousContactC76784);
        Assert.Equal(callbacks == 0 ? (byte)0 : (byte?)null, restored.WaterStepEdges2045C7C);
    }

    [Fact]
    public void ExplicitHistoricalValuesAndUnknownsSurviveDefaultCompletion()
    {
        GameState state = GameSession.CreateColdProcess().State;
        state = state with { WorldProcess = state.WorldProcess!.AdvanceMainLoopCallbacks(3) with { PreviousContactC76784 = 12, WaterStepEdges2045C7C = 3 } };
        JsonObject snapshot = JsonSerializer.SerializeToNode(state, Options)!.AsObject();
        Assert.Equal(JsonSerializer.Serialize(state.WorldProcess, Options), JsonSerializer.Serialize(Deserialize(snapshot).WorldProcess, Options));
        snapshot["WorldProcess"]!["PreviousContactC76784"] = null;
        snapshot["WorldProcess"]!["WaterStepEdges2045C7C"] = null;
        NativeWorldProcess restored = Deserialize(snapshot).WorldProcess!;
        Assert.Null(restored.PreviousContactC76784);
        Assert.Null(restored.WaterStepEdges2045C7C);
    }

    private static GameState Deserialize(JsonObject snapshot)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(snapshot.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(stream, Options);
    }
}
