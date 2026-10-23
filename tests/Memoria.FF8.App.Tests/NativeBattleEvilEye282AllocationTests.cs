using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEvilEye282AllocationTests
{
    [Fact]
    public void Original_dormant_scene_slot_allocates_and_publishes_current_bone_thirteen()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EVILEYE282_ALLOCATION_EVIDENCE");
        Assert.SkipUnless(directory is not null && output is not null, "Select installed original resources and a distinct allocation evidence directory.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(436);
        Assert.Equal(125, formation.StageId);
        BattleEnemyPlacement prototype = Assert.Single(formation.Slots, slot => slot.Slot == 3);
        Assert.Equal(94, prototype.ModelId);
        Assert.False(prototype.Enabled);
        Assert.True(prototype.Hidden);
        Assert.True(prototype.Untargetable);
        Assert.True(prototype.InitiallySuspended);
        Assert.Equal(new NativeBattleVector(0, 0, -5300), prototype.NativePosition);
        BattleNativeAction evilEye = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 152 && action.MagicId == 282);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(94, 1), evilEye));
        BattleNativeAction baselineAction = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 84 && action.MagicId == 45);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(54, 1), baselineAction));
        NativeEffectFixtureRequest request = new("evil-eye282-allocation-baseline", 45, 8, 84, 0, 1200, 240) { EncounterId = 436, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0 };
        Directory.CreateDirectory(output!);
        _ = NativeBattleEffectFixtureExporter.Export(request, directory!, Path.Combine(output!, "baseline"));
        BattleSnapshot baseline = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "baseline", "before-entry.battle.json"));
        Assert.Equal(436, baseline.Definition.EncounterId);
        Assert.Null(baseline.NativeGraphics!.OriginalEffect);
        (BattleSnapshot ready, int actualActor) = NativeBattleEvilEye282FixturePreparation.Prepare(game, assets, baseline, output!);
        NativeBattleGraphicsState graphics = ready.NativeGraphics ?? throw new InvalidDataException("The allocated component requires its actual graphics owner.");
        Assert.Null(graphics.OriginalEffect);
        Assert.InRange(actualActor, 3, 6);
        BattleSession session = BattleSession.Restore(ready);
        BattleCombatantDefinition actualDefinition = session.GetCombatantDefinition(graphics.ActorIds[actualActor] ?? throw new InvalidDataException("The actual allocation requires its physical actor identity."));
        Assert.Equal(94, actualDefinition.ModelId);
        Assert.Equal(3, actualDefinition.SceneSlot);
        NativeBattleGraphicsModel model = graphics.Models[actualActor];
        Assert.Equal((byte)110, model.ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(actualDefinition).PoseSource ?? throw new InvalidDataException("The allocated original model requires its current skeleton.");
        var matrices = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.True(matrices.Length > 13);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.ActiveResourceRow100);
        NativeBattlePackedMatrix32 selected = NativeBattlePackedMatrix32.FromMatrix(matrices[13], null);
        Assert.False(selected.Known[18]);
        Assert.False(selected.Known[19]);
        Assert.All(selected.Known.Where((_, index) => index is not (18 or 19)), known => Assert.True(known));
        Assert.Equal(prototype.NativePosition, model.Transform!.Position28);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(pose.Skeleton.AsSpan())), model.CurrentBodyBoneMatrices!.SkeletonSha256);
        Assert.Equal(JsonSerializer.Serialize(ready), JsonSerializer.Serialize(BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "allocation-ready.battle.json"))).Capture()));
        File.WriteAllText(Path.Combine(output!, "allocation-result.json"), JsonSerializer.Serialize(new { Status = "passed", Scenario = "Controlled original scene-slot3 allocation through the actual native producer; no ordinary three-death gameplay admission or Evil-Eye effect execution", BaselineAction = new { Effect = request.Effect, request.ActionId, request.CasterActorIndex, request.AdmissionProbeMaximumCallbacks }, ActualActor = actualActor, ResourceModel = actualDefinition.ModelId, actualDefinition.SceneSlot, PoseBoneCount = pose.BoneCount, CurrentBone13 = selected, CurrentWorld = model.GeometryWorldMatrix, SkeletonSha256 = model.CurrentBodyBoneMatrices!.SkeletonSha256 }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
