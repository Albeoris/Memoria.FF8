using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemySnapshotCompatibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Whole_plain_and_compact_load_preserve_absent_enemy_map_in_root_and_idle_battle(bool compact, bool idleBattle)
    {
        // Historical idle snapshots included these derived projections before their serialization was retired.
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type == typeof(NativeBattleGraphicsState))
            {
                JsonPropertyInfo? ignored = info.Properties.FirstOrDefault(property => property.Name == "Requests");
                if (ignored is not null)
                {
                    info.Properties.Remove(ignored);
                }
                JsonPropertyInfo requests = info.CreateJsonPropertyInfo(typeof(ImmutableArray<NativeBattleGraphicsRequest>), "Requests");
                requests.Get = owner => ((NativeBattleGraphicsState)owner).Requests;
                info.Properties.Add(requests);
            }
            if (info.Type == typeof(NativeBattleScriptState))
            {
                JsonPropertyInfo? ignored = info.Properties.FirstOrDefault(property => property.Name == "WaitCounters");
                if (ignored is not null)
                {
                    info.Properties.Remove(ignored);
                }
                JsonPropertyInfo counters = info.CreateJsonPropertyInfo(typeof(ushort[]), "WaitCounters");
                counters.Get = owner => ((NativeBattleScriptState)owner).WaitCounters;
                info.Properties.Add(counters);
            }
            if (info.Type == typeof(NativeBattleDescriptorState))
            {
                JsonPropertyInfo? ignored = info.Properties.FirstOrDefault(property => property.Name == "Queue");
                if (ignored is not null)
                {
                    info.Properties.Remove(ignored);
                }
                JsonPropertyInfo queue = info.CreateJsonPropertyInfo(typeof(NativeBattleDescriptorState).GetProperty("Queue")!.PropertyType, "Queue");
                queue.Get = owner => ((NativeBattleDescriptorState)owner).Queue;
                info.Properties.Add(queue);
            }
        });
        JsonSerializerOptions options = new() { TypeInfoResolver = resolver };
        GameState cold = GameSession.CreateColdProcess().State;
        NativeBattleRetainedMemory retained = NativeBattleStorageSnapshots.Restore(cold.NativeBattleMemory!);
        NativeBattleModelCpuResources cpu = retained.ModelCpuResources!;
        using JsonDocument defaults = JsonDocument.Parse(JsonSerializer.Serialize(cpu));
        Assert.Equal(JsonValueKind.Object, defaults.RootElement.GetProperty(nameof(NativeBattleModelCpuResources.EnemyResources)).ValueKind);
        NativeBattleModelCpuReference original = new(3, 32);
        cpu = cpu with { EnemyResources = null, EnemyCursor = 128, Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(12, original)) };
        using JsonDocument legacyCpu = JsonDocument.Parse(JsonSerializer.Serialize(cpu));
        Assert.False(legacyCpu.RootElement.TryGetProperty(nameof(NativeBattleModelCpuResources.EnemyResources), out _));
        retained = NativeBattleStorageSnapshots.Capture(retained with { ModelCpuResources = cpu, FileStaging = retained.FileStaging!.Write(original.SharedAddress, [0xA1, 0xB2]) });
        GameState historical = cold with { Scene = SceneKind.Field, FieldId = 165, NativeBattleMemory = retained };
        if (idleBattle)
        {
            BattleSession battle = new(new BattleDefinition
            {
                NativeRetainedMemory = retained,
                Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
            }, BattleAtbMode.Normal);
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
            snapshot = snapshot with
            {
                Definition = snapshot.Definition with { NativeGraphics = new(rules, [], []) },
                NativeGraphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0),
                NativePartyPresentation = new([null, null, null]),
                NativeModelCpuResources = cpu
            };
            historical = historical with { Battle = snapshot, NativeBattleMemory = NativeBattleRetainedMemory.Capture(snapshot, retained.ColdExecutableSha256) };
            using JsonDocument battleJson = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, options));
            JsonElement current = battleJson.RootElement;
            Assert.Equal(JsonValueKind.Null, current.GetProperty("ActiveAction").ValueKind);
            Assert.Equal(0, current.GetProperty("QueuedActions").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, current.GetProperty("NativeTimedAction").ValueKind);
            Assert.Equal(0, current.GetProperty("GuardianForceCasts").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, current.GetProperty("NativeActionScratch").GetProperty("ComponentCompletion").ValueKind);
            Assert.Equal(0, current.GetProperty("NativeScriptState").GetProperty("WaitCounters").GetArrayLength());
            Assert.False(current.GetProperty("NativeScriptState").GetProperty("AllocationControl").GetProperty("ActionGate").GetBoolean());
            Assert.Equal(JsonValueKind.Null, current.GetProperty("NativeGraphics").GetProperty("OriginalEffect").ValueKind);
            Assert.Equal(0, current.GetProperty("NativeGraphics").GetProperty("Descriptors").GetProperty("Queue").GetArrayLength());
            Assert.Equal(0, current.GetProperty("NativeGraphics").GetProperty("Requests").GetArrayLength());
        }
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-enemy-domain-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (FileStream output = File.Create(path))
            {
                if (compact)
                {
                    SnapshotStorage.Write(output, historical, options);
                }
                else
                {
                    JsonSerializer.Serialize(output, historical, options);
                }
            }
            byte[] source = File.ReadAllBytes(path);
            GameState loaded = GameSession.Load(path);
            NativeBattleModelCpuResources restored = loaded.NativeBattleMemory!.ModelCpuResources!;
            Assert.Null(restored.EnemyResources);
            Assert.Equal(original, restored.Rows[0].Require(12));
            Assert.Equal(new byte[] { 0xA1, 0xB2 }, NativeBattleModelEnemyHeap.Read(restored, loaded.NativeBattleMemory.FileStaging!, restored.Rows[0].Require(12), 2));
            if (idleBattle)
            {
                Assert.Null(loaded.Battle!.NativeModelCpuResources!.EnemyResources);
                Assert.Null(loaded.Battle.Definition.NativeRetainedMemory!.ModelCpuResources!.EnemyResources);
            }
            Assert.Equal(source, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
