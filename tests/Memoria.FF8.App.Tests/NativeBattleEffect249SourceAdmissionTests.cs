using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect249SourceAdmissionTests
{
    [Fact]
    public void Selected_source_preserves_current_resource_owners_and_complete_rigs()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT249_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current Effect249 source snapshot.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string before = JsonSerializer.Serialize(source);
        ValidateSource(source, game, assets, true);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        BattleSnapshot decoded = JsonSerializer.Deserialize<BattleSnapshot>(before) ?? throw new InvalidDataException("The selected source snapshot is null.");
        BattleSnapshot restored = BattleSession.Restore(decoded).Capture();
        Assert.Equal(before, JsonSerializer.Serialize(restored));
    }

    internal static void ValidateSource(BattleSnapshot source, GameAssets game, BattleAssets assets, bool cold)
    {
        ArgumentNullException.ThrowIfNull(game);
        Assert.Equal(194, source.Definition.EncounterId);
        Assert.Equal(52, source.Definition.StageId);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
        NativeBattleEffectMemory memory = Assert.IsType<NativeBattleEffectMemory>(source.NativeEffectMemory);
        NativeBattleModelCpuResources cpu = Assert.IsType<NativeBattleModelCpuResources>(source.NativeModelCpuResources);
        NativeBattleFileStaging staging = Assert.IsType<NativeBattleFileStaging>(source.NativeFileStaging);
        Assert.Equal(7, graphics.Models.Length);
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, memory.Active);
        Assert.NotNull(memory.ScriptedGraphics);
        Assert.NotNull(memory.ScriptedColorRegisters);
        Assert.NotNull(memory.PrimitiveProcess);
        Assert.NotNull(memory.Textures);
        Assert.NotNull(source.NativeFileIo);
        NativeBattleScriptedEffectMemory core = Assert.IsType<NativeBattleScriptedEffectMemory>(memory.ScriptedEffects);
        NativeBattleGeometryRegisters geometry = Assert.IsType<NativeBattleGeometryRegisters>(memory.GeometryRegisters);
        core.Validate();
        geometry.Validate();
        cpu.Validate();
        staging.Validate();
        if (cold)
        {
            Assert.Null(graphics.OriginalEffect);
            (byte[] bytes, bool[] known) = core.ReadSnapshot(NativeBattleScriptedEffectMemory.Address, NativeBattleScriptedEffectMemory.Length);
            Assert.All(known, value => Assert.True(value));
            Assert.All(bytes, value => Assert.Equal((byte)0, value));
            Assert.All(geometry.Rotation20, value => Assert.NotNull(value));
            Assert.All(geometry.Translation, value => Assert.NotNull(value));
            Assert.All(geometry.V1, value => Assert.NotNull(value));
            Assert.All(geometry.V2, value => Assert.NotNull(value));
            Assert.All(geometry.Sxy, value => Assert.NotNull(value));
            Assert.All(geometry.Sz, value => Assert.NotNull(value));
            Assert.All(geometry.AnchorScratch8, value => Assert.NotNull(value));
            Assert.NotNull(geometry.Mac0);
            Assert.NotNull(geometry.Otz);
        }
        foreach (int physical in new[] { 0, 3 })
        {
            string id = Assert.IsType<string>(graphics.ActorIds[physical]);
            BattleCombatantDefinition definition = session.GetCombatantDefinition(id);
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, candidate => candidate.Id == id);
            Assert.True(actor.NativeParticipant!.Active);
            Assert.False(actor.NativeParticipant.Hidden);
            Assert.False(actor.NativeParticipant.Suspended);
            Assert.False(actor.NativeParticipant.Untargetable);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Null(model.ReadLinkedModelIndex140());
            int rowIndex = Assert.IsType<int>(model.ActiveResourceRow100);
            Assert.Equal(physical == 3 ? 0 : 1, rowIndex);
            NativeBattleModelCpuResourceRow row = cpu.Rows[rowIndex];
            NativeBattleModelCpuReference skeleton = row.Require(12);
            Assert.Equal(physical == 3 ? (byte)3 : (byte)0, skeleton.Bank);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            if (physical == 3)
            {
                Assert.False(definition.IsPlayer);
                Assert.Equal(102, definition.ModelId);
                Assert.Equal((byte)118, model.ModelId4);
                Assert.Equal(0, definition.SceneSlot);
                Assert.Null(model.ActiveStagedResourceRow100);
                Assert.Equal(2176, row.Lengths[12]);
                Assert.Equal(32124, row.Lengths[16]);
                Assert.Equal(39660, row.Lengths[20]);
                Assert.Equal((byte)3, row.Require(16).Bank);
                Assert.Equal((byte)3, row.Require(20).Bank);
                byte[] skeletonFront = NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, 4);
                byte[] geometryFront = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(16), 4);
                byte[] animationFront = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(20), 4);
                Assert.Equal((byte)45, skeletonFront[0]);
                Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(geometryFront));
                Assert.Equal(21, BinaryPrimitives.ReadInt32LittleEndian(animationFront));
                resource = NativeBattleEnemyResourceView.Resolve(cpu, staging, rowIndex, resource);
            }
            else
            {
                Assert.True(definition.IsPlayer);
                Assert.Equal(0, definition.ModelId);
                Assert.Equal((byte)0, model.ModelId4);
            }
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
            pose.Validate();
            Assert.Equal(physical == 3 ? 45 : 23, pose.BoneCount);
            byte[] currentSkeleton = NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, pose.Skeleton.Length);
            Assert.True(pose.Skeleton.AsSpan().SequenceEqual(currentSkeleton));
            if (physical == 3)
            {
                Assert.Equal(2176, currentSkeleton.Length);
                Assert.Equal(16 + 48 * pose.BoneCount, currentSkeleton.Length);
            }
            Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
            Assert.All(model.CurrentBodyBoneMatrices!.Matrices, matrix => matrix.Validate());
            Assert.NotNull(model.GeometryWorldMatrix);
            model.GeometryWorldMatrix!.Validate();
            if (physical == 0)
            {
                NativeBattlePoseSource linked = Assert.IsType<NativeBattlePoseSource>(resource.LinkedPoseSource);
                linked.Validate();
                Assert.Equal(2, linked.BoneCount);
                Assert.Equal(2, NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = model.CurrentLinkedBoneMatrices }, linked).Length);
                Assert.All(model.CurrentLinkedBoneMatrices!.Matrices, matrix => matrix.Validate());
            }
            else
            {
                Assert.Null(model.CurrentLinkedBoneMatrices);
            }
        }
        foreach (int physical in new[] { 1, 2, 4, 5, 6 })
        {
            Assert.Equal(0, graphics.Models[physical].Flags0 & 2);
            Assert.Null(graphics.Models[physical].HasLinkedModelRing140);
        }
        NativeBattleStageState stage = Assert.IsType<NativeBattleStageState>(source.NativeStage);
        Assert.Equal(4, stage.Groups.Length);
        Assert.All(stage.Groups, group =>
        {
            Assert.Equal(7, group.Flags);
            if (cold)
            {
                Assert.Equal((short)0, group.ColorDepth);
                Assert.Equal(0u, group.ColorFlags);
            }
            Assert.NotNull(group.Source);
            group.Source!.Validate();
            Assert.Equal(2, group.Matrices.Length);
            Assert.All(group.Matrices, matrix => matrix.Validate());
        });
    }
}
