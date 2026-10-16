using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleScriptPoolMigrationTests
{
    [Fact]
    public void ExplicitlyMigrateOnlyTheNamedEmptyFrameZeroScriptBankAndValidateIndependentContinuation()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCRIPT_POOL_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCRIPT_POOL_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Explicit original and separately named script-pool migration paths are required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output!));
        JsonObject document = JsonNode.Parse(File.ReadAllText(input!))!.AsObject();
        Assert.Equal(0, document["Frame"]!.GetValue<int>());
        Assert.Equal(512, document["Definition"]!["EncounterId"]!.GetValue<int>());
        Assert.Null(document["NativeGraphics"]);
        Assert.Null(document["NativeClock"]);
        Assert.Null(document["NativeSupport"]);
        Assert.Empty(document["QueuedActions"]!.AsArray());
        Assert.Null(document["ActiveAction"]);
        JsonObject script = document["NativeScriptState"]!.AsObject();
        Assert.Null(script["Tasks"]);
        Assert.Empty(script["WaitCounters"]!.AsArray());
        Assert.Empty(script["PendingInitializations"]!.AsArray());
        script.Remove("WaitCounters");
        script.Remove("PendingInitializations");
        //This declared frame0 component has no callback history.482D10 establishes
        //only the empty links and completedBYTE15; retained payload remains unknown.
        script["Tasks"] = JsonSerializer.SerializeToNode(NativeBattleScriptTasks.Clear());
        foreach (JsonNode? combatant in document["Combatants"]!.AsArray())
        {
            JsonObject participant = combatant!["NativeParticipant"]!.AsObject();
            Assert.False(participant.ContainsKey("ReturnDamage"));
            //The declared frame0 component has no hit/action history.48C620
            //clears this WORD for all seven physical rows before initial AI.
            participant["ReturnDamage"] = 0;
        }
        BattleSnapshot migrated = JsonSerializer.Deserialize<BattleSnapshot>(document)!;
        BattleSession battle = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(battle.Capture()));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!))!);
        File.WriteAllText(output!, JsonSerializer.Serialize(battle.Capture(), new JsonSerializerOptions { WriteIndented = true }));
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(output!))!);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        battle.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        Assert.Equal(1, independent.Frame);
    }
}
