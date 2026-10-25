using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect312SourceAdmissionTests
{
    [Fact]
    public void Current_released_trio_and_three_party_resources_preserve_exclusive_cold_owners_without_advancing()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT312_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current post-prerequisite Effect312 component source.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        Assert.Equal(33, source.Frame);
        Assert.Equal(794, source.Definition.EncounterId);
        Assert.Equal(65, source.Definition.StageId);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.Equal(111, graphics.Callback);
        Assert.Null(graphics.OriginalEffect);
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        NativeBattleEffect312State state = Assert.IsType<NativeBattleEffect312State>(source.NativeEffectMemory.Effect312);
        state.Validate();
        Assert.True(state.ColdSourceOrigin);
        Assert.False(state.StaticPacketBound);
        Assert.Null(state.CapturePacket24);
        Assert.Equal(2, state.FixedBanks.Count);
        Assert.Equal(2, state.FixedQueues.Count);
        foreach (NativeBattleCureFamilyPool pool in Pools)
        {
            Assert.Equal(new byte[pool.Stride * pool.Capacity], state.ReadBank(pool.Bank).Read(0, pool.Stride * pool.Capacity));
            Assert.Equal(new byte[8], state.ReadQueue(pool.Descriptor).Read(0, 8));
        }
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(3, source.Definition.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(3, source.Definition.Combatants.Count(actor => !actor.IsPlayer && actor.InitialActive));
        int[] bodyBones = [23, 23, 34, 44, 6, 6];
        int[] weapons = [0, 7, 11];
        for (int physical = 0; physical < 6; physical++)
        {
            string actorId = graphics.ActorIds[physical]!;
            BattleCombatantDefinition definition = session.GetCombatantDefinition(actorId);
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, current => current.Id == actorId);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.True(actor.NativeParticipant!.Active);
            Assert.Equal((byte)physical, actor.NativeParticipant.ActorIndex);
            Assert.False(actor.NativeParticipant.Hidden);
            Assert.False(actor.NativeParticipant.Suspended);
            Assert.False(actor.NativeParticipant.Untargetable);
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Null(model.ReadLinkedModelIndex140());
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            resource.Validate();
            if (physical >= 3)
            {
                Assert.False(definition.IsPlayer);
                Assert.Equal(physical + 102, definition.ModelId);
                Assert.Equal(physical - 3, definition.SceneSlot);
                Assert.Equal((byte)(physical + 118), model.ModelId4);
                Assert.False(definition.InitialHidden);
                Assert.False(definition.InitialSuspended);
                Assert.False(definition.InitialUntargetable);
                resource = NativeBattleEnemyResourceView.Resolve(source.NativeModelCpuResources!, source.NativeFileStaging!, model.ActiveResourceRow100!.Value, resource);
            }
            else
            {
                Assert.True(definition.IsPlayer);
                Assert.Equal(physical, definition.ModelId);
                Assert.Equal(weapons[physical], definition.WeaponId);
                Assert.Equal((byte)physical, model.ModelId4);
                Assert.NotNull(resource.LinkedTexturePack);
                Assert.False(resource.LinkedTexturePack!.Data.IsDefaultOrEmpty);
                ImmutableArray<NativeBattleRawMesh> weapon = resource.LinkedGeometry!.Value;
                Assert.NotEmpty(weapon);
                Assert.All(weapon, mesh => mesh.Validate());
                if (resource.LinkedGeometryUsesBodySkeleton == true)
                {
                    Assert.All(weapon.SelectMany(mesh => mesh.Vertices), vertex => Assert.InRange((int)vertex.Bone, 0, bodyBones[physical] - 1));
                }
                else
                {
                    NativeBattlePoseSource linked = Assert.IsType<NativeBattlePoseSource>(resource.LinkedPoseSource);
                    linked.Validate();
                    Assert.NotEmpty(resource.LinkedAnimationFrames);
                    NativeBattleBodyBoneMatrices linkedMatrices = Assert.IsType<NativeBattleBodyBoneMatrices>(model.CurrentLinkedBoneMatrices);
                    linkedMatrices.Validate();
                    Assert.Equal(linked.BoneCount, linkedMatrices.Matrices.Length);
                }
            }
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
            Assert.Equal(bodyBones[physical], pose.BoneCount);
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, pose);
            Assert.Equal(bodyBones[physical], matrices.Length);
            Assert.All(matrices, matrix => matrix.Validate());
            model.GeometryWorldMatrix!.Validate();
        }
        NativeBattleStageState stage = Assert.IsType<NativeBattleStageState>(source.NativeStage);
        Assert.Equal(4, stage.Groups.Length);
        foreach (int physical in new[] { 0, 1 })
        {
            NativeBattleStageGroup group = stage.Groups[physical];
            Assert.Equal((byte)7, group.Flags);
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(group.Source);
            pose.Validate();
            Assert.Equal(2, pose.BoneCount);
            Assert.Equal(2, group.Matrices.Length);
            Assert.All(group.Matrices, matrix => matrix.Validate());
            NativeBattleDecodedPose decoded = pose.Sample(group.Animation, group.Counter, group.PoseFlags);
            Assert.Equal(2, decoded.Bones.Length);
            Assert.NotNull(stage.CurrentGroupGeometryAddress(physical, source.NativeFileStaging!.Read));
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 285 && row.MagicId == 312 && row.KernelSection == 3 && row.KernelIndex == 285);
        Assert.Equal(20, action.RawRecord.Length);
        NativeBattleEffect312Resources resources = game.ReadNativeEffect312Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([312])[311];
        Assert.Same(resources, binding.Effect312Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource(InitializerTextureName, InitializerGlobal), binding.InitializerResource);
        Assert.Null(source.NativeAiVariables);
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
    }
}
