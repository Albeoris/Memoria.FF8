using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect238SourceAdmissionTests
{
    [Fact]
    public void Selected_source_preserves_actual_current_rigs_and_shared_process_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT238_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current Effect238 source snapshot.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string before = JsonSerializer.Serialize(source);
        ValidateSource(source, game, assets, true);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Equal(before, JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
    }

    internal static void ValidateSource(BattleSnapshot source, GameAssets game, BattleAssets assets, bool cold)
    {
        Assert.Equal(117, source.Definition.EncounterId);
        Assert.Equal(32, source.Definition.StageId);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.Equal(7, graphics.Models.Length);
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        Assert.NotNull(source.NativeEffectMemory.ScriptedEffects);
        Assert.NotNull(source.NativeEffectMemory.ScriptedGraphics);
        Assert.NotNull(source.NativeEffectMemory.ScriptedColorRegisters);
        Assert.NotNull(source.NativeEffectMemory.GeometryRegisters);
        Assert.NotNull(source.NativeEffectMemory.PrimitiveProcess);
        Assert.NotNull(source.NativeEffectMemory.Textures);
        Assert.NotNull(source.NativeModelCpuResources);
        Assert.NotNull(source.NativeFileStaging);
        Assert.NotNull(source.NativeFileIo);
        if (cold)
        {
            Assert.Null(graphics.OriginalEffect);
        }
        foreach (int physical in new[] { 0, 3 })
        {
            string id = graphics.ActorIds[physical]!;
            BattleCombatantDefinition definition = session.GetCombatantDefinition(id);
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, candidate => candidate.Id == id);
            Assert.True(actor.NativeParticipant!.Active);
            Assert.False(actor.NativeParticipant.Hidden);
            Assert.False(actor.NativeParticipant.Suspended);
            Assert.False(actor.NativeParticipant.Untargetable);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Null(model.ReadLinkedModelIndex140());
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            if (physical == 3)
            {
                Assert.Equal(52, definition.ModelId);
                Assert.Equal((byte)68, model.ModelId4);
                Assert.Equal(0, definition.SceneSlot);
                resource = NativeBattleEnemyResourceView.Resolve(source.NativeModelCpuResources!, source.NativeFileStaging!, model.ActiveResourceRow100!.Value, resource);
            }
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
            pose.Validate();
            Assert.Equal(physical == 3 ? 41 : 23, pose.BoneCount);
            Assert.Equal(physical == 3 ? (byte)27 : (byte)9, pose.Skeleton[4]);
            Assert.Equal((byte)1, pose.Skeleton[5]);
            Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
            Assert.All(model.CurrentBodyBoneMatrices!.Matrices, matrix => matrix.Validate());
            model.GeometryWorldMatrix!.Validate();
            if (physical == 0)
            {
                Assert.Equal(2, Assert.IsType<NativeBattlePoseSource>(resource.LinkedPoseSource).BoneCount);
                Assert.Equal(2, model.CurrentLinkedBoneMatrices!.Matrices.Length);
            }
        }
        foreach (int physical in new[] { 1, 2, 4, 5, 6 })
        {
            Assert.Equal(0, graphics.Models[physical].Flags0 & 2);
            Assert.Null(graphics.Models[physical].HasLinkedModelRing140);
        }
        Assert.Equal(4, source.NativeStage!.Groups.Length);
        Assert.All(source.NativeStage.Groups, group =>
        {
            Assert.Equal(2, group.Source!.BoneCount);
            Assert.Equal(2, group.Matrices.Length);
        });
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([238])[237];
        Assert.Equal(0xA1FDC0u, binding.Entry);
        Assert.Equal(0xA1FD90u, binding.Initializer);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(238);
        Assert.Equal(0xA20430u, profile.Callback);
        Assert.Equal(0xA25630u, profile.LoadCallback);
    }
}
