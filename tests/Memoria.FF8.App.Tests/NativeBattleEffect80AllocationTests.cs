using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect80AllocationTests
{
    [Fact]
    public void Original_participant_release_and_scene_allocation_prepare_current_target_and_caster()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT80_ALLOCATION_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT80_ALLOCATION_EVIDENCE");
        Assert.SkipUnless(directory is not null && source is not null && output is not null, "Select installed resources, an original formation511 entry-complete source and a distinct allocation evidence directory.");
        Assert.False(File.Exists(Path.Combine(output!, "allocation-receipt.json")), "Select a new allocation evidence directory.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot baseline = NativeBattleEffectFamilyRunner.LoadSnapshot(source!);
        Assert.Equal(511, baseline.Definition.EncounterId);
        Assert.Equal(new[] { 121, 123 }, baseline.Definition.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.ModelId).Order().ToArray());
        (BattleSnapshot ready, int caster, int target) = NativeBattleEffect80FixturePreparation.Prepare(game, assets, baseline, output!);
        Assert.Equal(4, caster);
        Assert.InRange(target, 3, 6);
        Assert.NotEqual(caster, target);
        BattleSnapshot saved = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "allocation-ready.battle.json"));
        Assert.Equal(JsonSerializer.Serialize(ready), JsonSerializer.Serialize(saved));
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(BattleSession.Restore(saved).Capture()));
        Assert.NotNull(saved.NativeAiVariables);
        Assert.Equal(8, saved.NativeAiVariables!.Length);
        Assert.NotNull(Assert.Single(saved.Definition.Combatants, actor => actor.ModelId == 124).NativeAi);
        Assert.True(File.Exists(Path.Combine(output!, "source-before-entry.battle.json")));
        Assert.True(File.Exists(Path.Combine(output!, "fresh-source-origin.json")));
        Assert.True(File.Exists(Path.Combine(output!, "participant-release-receipt.json")));
        Assert.Null(saved.NativeGraphics!.OriginalEffect);
        Assert.Null(saved.ActiveAction);
        Assert.Empty(saved.QueuedActions);
        Assert.Contains(saved.Events, entry => entry.Kind == "NativeSpawn");
        File.WriteAllText(Path.Combine(output!, "allocation-result.json"), JsonSerializer.Serialize(new { Status = "passed", Scenario = "Original-resource controlled allocation and activation prerequisite; no effect80 execution or ordinary preceding death/AI/formula admission", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))), ReadySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output!, "allocation-ready.battle.json")))), FormationId = 511, StageId = 142, CasterActorIndex = caster, TargetActorIndex = target, saved.Frame, NativeCallback = saved.NativeGraphics!.Callback }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
