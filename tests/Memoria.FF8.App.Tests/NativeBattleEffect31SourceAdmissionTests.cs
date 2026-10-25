using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect31SourceAdmissionTests
{
    [Fact]
    public void Fresh_Heartbreak_source_retains_actual_models_aliases_and_exclusive_cold_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT31_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current Effect31 source snapshot.");
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
        Assert.Equal(110, source.Definition.EncounterId);
        Assert.Equal(24, source.Definition.StageId);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.Equal(7, graphics.Models.Length);
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        if (cold)
        {
            Assert.Null(graphics.OriginalEffect);
        }
        NativeBattleEffect31State state = Assert.IsType<NativeBattleEffect31State>(source.NativeEffectMemory.Effect31);
        state.Validate();
        Assert.Equal(7, state.FixedBanks.Count);
        Assert.Equal(7, state.FixedQueues.Count);
        Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.All(state.FixedQueues.Values, queue => Assert.NotNull(queue));
        if (cold)
        {
            Assert.True(state.ColdSourceOrigin);
            Assert.False(state.StaticSourcesBound);
            foreach (NativeBattleCureFamilyPool shape in Pools)
            {
                Assert.Equal(new byte[shape.Stride * shape.Capacity], state.ReadBank(shape.Bank).Read(0, shape.Stride * shape.Capacity));
                Assert.Equal(new byte[8], state.ReadQueue(shape.Descriptor).Read(0, 8));
            }
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
            Assert.Equal((byte)physical, actor.NativeParticipant.ActorIndex);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Null(model.ReadLinkedModelIndex140());
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            if (physical == 3)
            {
                Assert.Equal(23, definition.ModelId);
                Assert.Equal((byte)39, model.ModelId4);
                Assert.Equal(0, definition.SceneSlot);
                resource = NativeBattleEnemyResourceView.Resolve(source.NativeModelCpuResources!, source.NativeFileStaging!, model.ActiveResourceRow100!.Value, resource);
            }
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
            pose.Validate();
            Assert.Equal(physical == 3 ? 42 : 23, pose.BoneCount);
            Assert.Equal(physical == 3 ? (byte)3 : (byte)9, pose.Skeleton[4]);
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
        }
        Assert.Equal(4, source.NativeStage!.Groups.Length);
        Assert.All(source.NativeStage.Groups, group =>
        {
            Assert.Equal(2, group.Source!.BoneCount);
            Assert.Equal(2, group.Matrices.Length);
        });
        NativeBattleEffect31Resources resources = game.ReadNativeEffect31Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([31])[30];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Same(resources, binding.Effect31Resources);
    }
}
