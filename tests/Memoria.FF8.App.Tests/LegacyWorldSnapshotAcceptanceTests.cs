using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LegacyWorldSnapshotAcceptanceTests
{
    [Fact]
    public void HistoricalFieldEntryRestoresDormantWorldMapSelectionWithoutRewritingOrAdvancing()
    {
        string source = TestResources.FixturePath("menu-battle-field-entry");
        byte[] original = File.ReadAllBytes(source);
        using JsonDocument saved = JsonDocument.Parse(original);
        JsonElement root = saved.RootElement;
        JsonElement departed = root.GetProperty("DepartingWorldState");
        JsonElement scripted = departed.GetProperty("NativeScriptedRuntime");
        Assert.False(scripted.TryGetProperty("MapDisplayMode", out _));
        byte expected = scripted.GetProperty("Load").GetProperty("MapDisplayMode").GetByte();

        GameSession session = new(GameSession.Load(source));

        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, session.State.SnapshotSchemaVersion);
        Assert.Equal(expected, session.State.DepartingWorldState!.NativeScriptedRuntime!.MapDisplayMode);
        Assert.Equal(root.GetProperty("FieldId").GetInt32(), session.State.FieldId);
        Assert.Equal(departed.GetProperty("Frame").GetInt64(), session.State.DepartingWorldState.Frame);
        Assert.Equal(scripted.GetProperty("Path").GetProperty("Callback").GetInt64(), session.State.DepartingWorldState.NativeScriptedRuntime.Path.Callback);
        JsonElement restoredEvents = session.State.EventState!.Value;
        Assert.Equal(root.GetProperty("EventState").GetProperty("Frame").GetInt64(), restoredEvents.GetProperty("Frame").GetInt64());
        Assert.True(JsonElement.DeepEquals(root.GetProperty("EventState").GetProperty("Scene").GetProperty("FieldCallbackClock"), restoredEvents.GetProperty("Scene").GetProperty("FieldCallbackClock")));
        Assert.Equal(SHA256.HashData(original), SHA256.HashData(File.ReadAllBytes(source)));
    }
}
