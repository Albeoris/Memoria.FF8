using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleActionPacketMigrationTests
{
    [Fact]
    public void ExplicitlyMigrateTheNamedEmptyBoundaryWithUnknownRetainedMemory()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_RETAINED_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RETAINED_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Explicit original and separately named retained-memory migration paths are required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output!));
        Assert.False(File.Exists(output!), "Preserve existing standalone snapshots; choose a distinct output path.");
        byte[] bytes = File.ReadAllBytes(input!);
        const string sourceHash = "9C90114438C61408856313419BC8C24695F7152F5FBF67AEFF992BA075AF3204";
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(bytes)));
        JsonObject document = JsonNode.Parse(bytes)!.AsObject();
        Assert.Equal(0, document["Frame"]!.GetValue<int>());
        Assert.Equal(512, document["Definition"]!["EncounterId"]!.GetValue<int>());
        Assert.Null(document["NativeGraphics"]);
        Assert.Null(document["NativeClock"]);
        Assert.Null(document["ActiveAction"]);
        Assert.Empty(document["QueuedActions"]!.AsArray());
        Assert.Empty(document["Events"]!.AsArray());
        Assert.False(document.ContainsKey("NativeActionScratch"));
        //Empty/released physical banks have actorFF, zero targets and released links; retained packet bytes stay unknown.
        document["NativeCommandBanks"] = JsonSerializer.SerializeToNode(NativeBattleCommandBanks.Initialize());
        document["NativePartyStatusMirrors"] = JsonSerializer.SerializeToNode(NativeBattlePartyStatusMirrors.Unknown());
        document["NativeActionScratch"] = JsonSerializer.SerializeToNode(new NativeBattleActionScratch());
        document["NativePartyPresentation"] = null;
        document["NativeEffectIoResult"] = null;
        foreach (JsonNode? combatant in document["Combatants"]!.AsArray())
        {
            JsonObject status = combatant!["NativeStatus"]!.AsObject();
            Assert.False(status.ContainsKey("Presentation"));
            status["Presentation"] = JsonSerializer.SerializeToNode(new NativeBattleStatusMirror(null, null));
        }
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(document)!;
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!))!);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, battle.Capture(), new JsonSerializerOptions { WriteIndented = true });
        }
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(output!))!);
        battle.Tick();
        independent.Tick();
        Assert.Equal(1, independent.Frame);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input!))));
    }

    [Fact]
    public void ExplicitlyMigrateTheNamedPreActionBoundaryWithoutReconstructingPhysicalHistory()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACTION_PACKET_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACTION_PACKET_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Explicit original and separately named packet migration paths are required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output!));
        Assert.False(File.Exists(output!), "Preserve existing standalone snapshots; choose a distinct output path.");
        byte[] bytes = File.ReadAllBytes(input!);
        Assert.Equal("C92543F83F341AC3A7195630F2D7C25A30D1D35905F1E879F8ACA93A49F5DE71", Convert.ToHexString(SHA256.HashData(bytes)));
        JsonObject document = JsonNode.Parse(bytes)!.AsObject();
        Assert.Equal(0, document["Frame"]!.GetValue<int>());
        Assert.Equal(512, document["Definition"]!["EncounterId"]!.GetValue<int>());
        Assert.Null(document["NativeGraphics"]);
        Assert.Null(document["NativeClock"]);
        Assert.Null(document["NativeSupport"]);
        Assert.Null(document["NativeTimedAction"]);
        Assert.Null(document["ActiveAction"]);
        Assert.Empty(document["QueuedActions"]!.AsArray());
        Assert.Empty(document["Events"]!.AsArray());
        Assert.False(document.ContainsKey("NativeActionPackets"));
        //This named pre-action boundary has no published header/contact history.48D1F0 clears counts only.
        document["NativeActionPackets"] = JsonSerializer.SerializeToNode(NativeBattleActionPacketsState.Fresh());
        //No inferred dispatch-order or remaining-hit BYTE: the next actual485460/48EB40 writer owns them.
        document["NativeReactionOrder"] = null;
        foreach (JsonNode? combatant in document["Combatants"]!.AsArray())
        {
            JsonObject participant = combatant!["NativeParticipant"]!.AsObject();
            Assert.False(participant.ContainsKey("PendingReaction"));
            Assert.False(participant.ContainsKey("RemainingHits"));
            bool active = participant["Active"]!.GetValue<bool>();
            participant["PendingReaction"] = JsonSerializer.SerializeToNode(active ? new NativeBattlePendingReaction(0, 0) : new NativeBattlePendingReaction(null, null));
            participant["RemainingHits"] = null;
        }
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(document)!;
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!))!);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, battle.Capture(), new JsonSerializerOptions { WriteIndented = true });
        }
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(output!))!);
        battle.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        Assert.Equal(1, independent.Frame);
        Assert.Equal("C92543F83F341AC3A7195630F2D7C25A30D1D35905F1E879F8ACA93A49F5DE71", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input!))));
    }

    [Fact]
    public void ResumeTheNamedActionPacketComponentAtItsCurrentBoundary()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACTION_PACKET_COMPONENT_SNAPSHOT");
        Assert.SkipUnless(path is not null, "A concrete standalone action-packet component is required.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!))!;
        Assert.Equal(512, snapshot.Definition.EncounterId);
        Assert.Equal(0, snapshot.Frame);
        Assert.Empty(snapshot.QueuedActions);
        Assert.Null(snapshot.ActiveAction);
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        BattleSession independent = BattleSession.Restore(battle.Capture());
        battle.Tick();
        independent.Tick();
        Assert.Equal(1, battle.Frame);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
    }
}
