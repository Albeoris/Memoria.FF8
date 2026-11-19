using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticModuleOwnersTests
{
    [Fact]
    public void Active_module_identities_are_bounded_scalar_copies_without_payload()
    {
        BattleSession logical = new(new() { Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        BattleModelOwner owner = new BattlePartyActorModel(new("ally"));
        NativeBattleModuleState modules = NativeBattleModuleState.Clear();
        var slots = modules.Slots.ToBuilder();
        for (int index = 0; index < 20; index++)
        {
            slots[index] = new(1, (ushort)(index + 10), index == 19 ? null : index + 1, NativeBattleModuleKind.CriticalFlash571300, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()) { ModelOwner = owner };
        }
        modules = modules with { Slots = slots.ToImmutable(), Head = 0, Tail = 19, PublishedCount = 20 };
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0)) with { ModuleEffects = modules };
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(logical, graphics);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess());
        object observed = process.ObserveDiagnosticGraphicsOwners();
        JsonElement json = JsonSerializer.SerializeToElement(observed).GetProperty("Graphics");
        Assert.Equal(20, json.GetProperty("ModuleCount").GetInt32());
        JsonElement tasks = json.GetProperty("ModuleTasks");
        Assert.Equal(16, tasks.GetArrayLength());
        Assert.Equal(0, tasks[0].GetProperty("Index").GetInt32());
        Assert.Equal(15, tasks[15].GetProperty("Index").GetInt32());
        Assert.Equal(25, tasks[15].GetProperty("State").GetInt32());
        Assert.Equal("ally", tasks[0].GetProperty("ModelOwner").GetString());
        Assert.False(tasks[0].TryGetProperty("Payload", out _));
        string before = JsonSerializer.Serialize(observed);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics with { ModuleEffects = NativeBattleModuleState.Clear() });
        Assert.Equal(before, JsonSerializer.Serialize(observed));
    }
}
