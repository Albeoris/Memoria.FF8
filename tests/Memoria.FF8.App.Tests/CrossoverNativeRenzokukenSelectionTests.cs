using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeRenzokukenSelectionTests
{
    [Fact]
    public void Original_crisis_and_finisher_draws_are_retained_per_host_turn()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVIII installation.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(game.ReadNewGameCharacterVitals(), assets);
        Ff7BattleAiActor source = new(0, 0, 0, 0, 0, 1, (uint)kit.MaximumHitPoints);
        NativeBattleRandomState native = NativeBattleRandomState.ForecastPhase(1);
        CrossoverNativeRenzokukenSelection initial = new(native);
        CrossoverNativeRenzokukenSelection disabled = initial.ObserveReady(100, source, 1, true, kit);
        Assert.Same(native, disabled.Random);
        Assert.Equal(0, disabled.Crisis);

        CrossoverNativeRenzokukenSelection ready = initial.ObserveReady(100, source, 1, false, kit);
        Assert.Equal(4, ready.Crisis);
        Assert.Same(ready, ready.ObserveReady(100, source, 1, false, kit));
        CrossoverNativeRenzokukenSelection cold = JsonSerializer.Deserialize<CrossoverNativeRenzokukenSelection>(JsonSerializer.Serialize(ready))!;
        cold.Validate(100);
        CrossoverNativeRenzokukenSelection selected = cold.Select(kit.FinisherFlags, out CrossoverNativeRenzokukenPlan plan);
        Assert.Equal(new CrossoverNativeRenzokukenPlan(141, 0), plan);
        Assert.Equal(native.Indexes[0] + 4, selected.Random.Indexes[0]);
        Assert.Null(selected.ReadyTick);

        Ff7BattleGuestPhysicalHit untimed = CrossoverNativeRenzokukenPlan.ProjectContact(kit, 1);
        Ff7BattleGuestPhysicalHit triggered = CrossoverNativeRenzokukenPlan.ProjectContact(kit, 25);
        Assert.Equal(assets.ReadPlayerNativeStatRules(game.ReadNewGameCharacterVitals(), kit.RecordRow).CharacterRecord[33], kit.RenzokukenPower);
        Assert.Equal(kit.RenzokukenPower * 16 / 20, untimed.Attack.Power);
        Assert.Equal(kit.RenzokukenPower * 24 / 20, triggered.Attack.Power);
        Assert.True(triggered.Attack.AlwaysHits);
        Assert.True(triggered.Attack.SuppressCritical);
    }
}
