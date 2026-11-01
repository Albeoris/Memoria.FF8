using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotOwnershipTests
{
    [Fact]
    public void InactiveResourceExtentsAreDiscardedWithoutChangingPhysicalOwnersOrSource()
    {
        GameState original = WithResourceExtent();
        NativeBattleRetainedMemory memory = original.NativeBattleMemory!;
        NativeBattleModelCpuResources resources = memory.ModelCpuResources!;

        GameState prepared = SnapshotOwnership.Prepare(original);

        Assert.Empty(prepared.NativeBattleMemory!.ModelCpuResources!.Rows[0].Lengths);
        Assert.Equal(128, resources.Rows[0].Lengths[16]);
        Assert.Same(resources.Cpu, prepared.NativeBattleMemory.ModelCpuResources.Cpu);
        Assert.Same(resources.Rows[0].Fields, prepared.NativeBattleMemory.ModelCpuResources.Rows[0].Fields);
        Assert.Same(memory.FileStaging, prepared.NativeBattleMemory.FileStaging);
        Assert.Same(memory.StorageGraph, prepared.NativeBattleMemory.StorageGraph);
        Assert.Equal(new byte[] { 7, 8, 9 }, prepared.NativeBattleMemory.ModelCpuResources.Cpu.Banks[0].Read(0, 3));
        Assert.Same(prepared, SnapshotOwnership.Prepare(prepared));
    }

    [Fact]
    public void ActiveBattleKeepsItsExactResourceMetadata()
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        GameState original = WithResourceExtent() with { Battle = battle.Capture() };

        Assert.Same(original, SnapshotOwnership.Prepare(original));
        Assert.Equal(128, original.NativeBattleMemory!.ModelCpuResources!.Rows[0].Lengths[16]);
    }

    [Fact]
    public void SaveOmitsInactiveDerivedExtentAndLoadRetainsPhysicalBytes()
    {
        GameSession session = new(WithResourceExtent());
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            session.Save(path);

            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement row = json.RootElement.GetProperty(nameof(GameState.NativeBattleMemory)).GetProperty(nameof(NativeBattleRetainedMemory.ModelCpuResources)).GetProperty(nameof(NativeBattleModelCpuResources.Rows))[0];
            Assert.False(row.TryGetProperty(nameof(NativeBattleModelCpuResourceRow.Lengths), out _));
            GameState loaded = GameSession.Load(path);
            Assert.Empty(loaded.NativeBattleMemory!.ModelCpuResources!.Rows[0].Lengths);
            Assert.Equal(new byte[] { 7, 8, 9 }, loaded.NativeBattleMemory.ModelCpuResources.Cpu.Banks[0].Read(0, 3));
            Assert.Equal(128, session.State.NativeBattleMemory!.ModelCpuResources!.Rows[0].Lengths[16]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnsupportedDerivedOwnershipRequiresAnImplementedContract()
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(SnapshotOwnership.ConfigureSerialization);
        JsonSerializerOptions options = new() { TypeInfoResolver = resolver };

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new UnsupportedDerivedOwner(), options));
    }

    private static GameState WithResourceExtent()
    {
        GameState state = GameSession.CreateColdProcess().State;
        NativeBattleRetainedMemory memory = state.NativeBattleMemory!;
        NativeBattleModelCpuResources resources = memory.ModelCpuResources!;
        NativeBattleModelCpuResourceRow row = resources.Rows[0].Set(16, new(0, 32)) with { Lengths = ImmutableDictionary<int, int>.Empty.Add(16, 128) };
        NativeBattleModelCpuBanks cpu = resources.Cpu with { Banks = resources.Cpu.Banks.SetItem(0, resources.Cpu.Banks[0].Write(0, [7, 8, 9])) };
        return state with { NativeBattleMemory = memory with { ModelCpuResources = resources with { Cpu = cpu, Rows = resources.Rows.SetItem(0, row) } } };
    }

    private sealed class UnsupportedDerivedOwner
    {
        [SnapshotOwnership(SnapshotLifetime.BattleResource, SnapshotAuthority.Derived, SnapshotReconstruction.None)]
        public int Value { get; init; }
    }
}
