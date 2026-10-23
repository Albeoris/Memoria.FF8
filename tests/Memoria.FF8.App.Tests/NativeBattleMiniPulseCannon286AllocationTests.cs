using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMiniPulseCannon286AllocationTests
{
    [Fact]
    public void Original_false_allocation_then_native_activation_publishes_current_bone_two()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MINIPULSECANNON286_ALLOCATION_EVIDENCE");
        Assert.SkipUnless(directory is not null && output is not null, "Select installed original resources and a distinct allocation evidence directory.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(431);
        Assert.Equal(126, formation.StageId);
        Assert.Equal(109, Assert.Single(formation.Enemies).ModelId);
        BattleNativeAction baselineAction = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 290 && action.MagicId == 101);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(109, 1), baselineAction));
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 293 && row.MagicId == 286);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(110, 1), action));
        NativeEffectFixtureRequest request = new("mini-pulse286-allocation-baseline", 101, 8, 290, 0, 1200, 240) { EncounterId = 431, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0 };
        Directory.CreateDirectory(output!);
        _ = NativeBattleEffectFixtureExporter.Export(request, directory!, Path.Combine(output!, "baseline"));
        BattleSnapshot baseline = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "baseline", "before-entry.battle.json"));
        Assert.Equal(431, baseline.Definition.EncounterId);
        Assert.Null(baseline.NativeGraphics!.OriginalEffect);
        (BattleSnapshot ready, int actualActor) = NativeBattleMiniPulseCannon286FixturePreparation.Prepare(game, assets, baseline, output!);
        NativeBattleGraphicsState graphics = ready.NativeGraphics ?? throw new InvalidDataException("The actual activated allocation requires its graphics owner.");
        Assert.Null(graphics.OriginalEffect);
        BattleSession session = BattleSession.Restore(ready);
        BattleCombatantDefinition definition = session.GetCombatantDefinition(graphics.ActorIds[actualActor] ?? throw new InvalidDataException("The actual activated allocation requires its actor identity."));
        Assert.Equal(110, definition.ModelId);
        Assert.Equal(0, definition.SceneSlot);
        Assert.Equal(actualActor, ready.NativeLastAllocatedIndex);
        NativeBattleGraphicsModel model = graphics.Models[actualActor];
        Assert.Equal((byte)126, model.ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(definition).PoseSource ?? throw new InvalidDataException("The actual activated original model requires its skeleton.");
        var matrices = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.True(matrices.Length > 2);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrices[2], null);
        Assert.False(packed.Known[18]);
        Assert.False(packed.Known[19]);
        Assert.Equal(new NativeBattleVector(-2000, 0, -4000), model.Transform!.Position28);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.Equal(JsonSerializer.Serialize(ready), JsonSerializer.Serialize(BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "allocation-ready.battle.json"))).Capture()));
        File.WriteAllText(Path.Combine(output!, "allocation-result.json"), JsonSerializer.Serialize(new { Status = "passed", Scenario = "Controlled original prototype0 allocation52(false), real descriptor statistics/load, then actual53 native activation; preserves prototype2 and companion109, no ordinary AI/action290 visual or effect286 execution", ActualActor = actualActor, ResourceModel = definition.ModelId, definition.SceneSlot, CurrentBoneCount = pose.BoneCount, SelectedBone = 2, CurrentBone2 = packed, CurrentWorld = model.GeometryWorldMatrix }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
