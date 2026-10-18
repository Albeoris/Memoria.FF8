using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshProcessSchemaTests
{
    [Fact]
    public void Cold_process_serializes_explicit_trail_banks_and_missing_imported_field_does_not_hydrate()
    {
        NativeBattleGilgameshProcess process = NativeBattleGilgameshProcess.FromColdProcess();
        process.Validate();
        string json = JsonSerializer.Serialize(process);
        JsonObject document = JsonNode.Parse(json)!.AsObject();
        Assert.True(document.ContainsKey(nameof(NativeBattleGilgameshProcess.SwordTrail)));
        Assert.NotNull(document[nameof(NativeBattleGilgameshProcess.SwordTrail)]![nameof(NativeBattleGilgameshSwordTrailState.Memory)]);
        NativeBattleGilgameshProcess restored = JsonSerializer.Deserialize<NativeBattleGilgameshProcess>(json)!;
        restored.Validate();
        foreach ((int offset, int length) in new[] { (NativeBattleGilgameshSwordTrailState.GatherA, 80), (NativeBattleGilgameshSwordTrailState.GatherB, 80), (NativeBattleGilgameshSwordTrailState.CurveA, 256), (NativeBattleGilgameshSwordTrailState.CurveB, 256), (NativeBattleGilgameshSwordTrailState.History, 200) })
        {
            Assert.Equal(new byte[length], restored.SwordTrail.Memory.Read(offset, length));
        }
        Assert.True(document.Remove(nameof(NativeBattleGilgameshProcess.SwordTrail)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleGilgameshProcess>(document.ToJsonString()));
    }

    [Fact]
    public void Explicit_null_imported_trail_is_rejected_without_replacing_its_process_owner()
    {
        JsonObject document = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleGilgameshProcess.FromColdProcess()))!.AsObject();
        document[nameof(NativeBattleGilgameshProcess.SwordTrail)] = null;
        NativeBattleGilgameshProcess imported = JsonSerializer.Deserialize<NativeBattleGilgameshProcess>(document.ToJsonString())!;
        Assert.Null(imported.SwordTrail);
        Assert.Throws<InvalidDataException>(() => imported.Validate());
    }
}
