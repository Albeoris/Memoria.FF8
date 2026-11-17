using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect256SourceAdmissionTests
{
    [Fact]
    public void Selected_source_preserves_actual_current_rigs_and_shared_process_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT256_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current Effect256 source snapshot.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleFixtureInspection.ReadLogicalSnapshot(path!);
        string before = JsonSerializer.Serialize(source);
        ValidateSource(source, game, assets, true);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Equal(before, JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
    }

    internal static void ValidateSource(BattleSnapshot source, GameAssets game, BattleAssets assets, bool cold)
    {
        Assert.Equal(378, source.Definition.EncounterId);
        Assert.Equal(98, source.Definition.StageId);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        NativeBattleGraphicsState graphics = source.ReadFixturePresentation().NativeGraphics!;
        Assert.Equal(7, ReadOriginalModelCells(graphics).Length);
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, source.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.NotNull(source.ReadFixturePresentation().NativeEffectMemory!.ScriptedEffects);
        Assert.NotNull(source.ReadFixturePresentation().NativeEffectMemory!.ScriptedGraphics);
        Assert.NotNull(source.ReadFixturePresentation().NativeEffectMemory!.ScriptedColorRegisters);
        Assert.NotNull(source.ReadFixturePresentation().NativeEffectMemory!.GeometryRegisters);
        Assert.NotNull(source.ReadFixturePresentation().NativeEffectMemory!.PrimitiveProcess);
        Assert.NotNull(source.ReadFixturePresentation().NativeEffectMemory!.Textures);
        Assert.NotNull(source.ReadFixturePresentation().NativeModelCpuResources);
        Assert.NotNull(source.ReadFixturePresentation().NativeFileStaging);
        Assert.NotNull(source.ReadFixturePresentation().NativeFileIo);
        if (cold)
        {
            Assert.Null(graphics.OriginalEffect);
        }
        foreach (int physical in new[] { 0, 3 })
        {
            string id = ReadOriginalModelActorId(graphics, physical)!;
            BattleCombatantDefinition definition = session.GetCombatantDefinition(id);
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, candidate => candidate.Id == id);
            Assert.True(actor.NativeParticipant!.Active);
            Assert.False(actor.NativeParticipant.Hidden);
            Assert.False(actor.NativeParticipant.Suspended);
            Assert.False(actor.NativeParticipant.Untargetable);
            NativeBattleGraphicsModel model = ReadOriginalModelCell(graphics, physical);
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Null(model.ReadLinkedModel());
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            if (physical == 3)
            {
                Assert.Equal(44, definition.ModelId);
                Assert.Equal((byte)60, model.ModelId4);
                Assert.Equal(0, definition.SceneSlot);
                resource = NativeBattleEnemyResourceView.Resolve(source.ReadFixturePresentation().NativeModelCpuResources!, source.ReadFixturePresentation().NativeFileStaging!, model.ActiveResourceRow100!.Value, resource);
            }
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
            pose.Validate();
            Assert.Equal(physical == 3 ? 40 : 23, pose.BoneCount);
            Assert.Equal(physical == 3 ? (byte)13 : (byte)9, pose.Skeleton[4]);
            Assert.Equal(physical == 3 ? (byte)0 : (byte)1, pose.Skeleton[5]);
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
            Assert.Equal(0, ReadOriginalModelCell(graphics, physical).Flags0 & 2);
            Assert.Null(ReadOriginalModelCell(graphics, physical).HasLinkedModelRing140);
        }
        Assert.Equal(4, source.ReadFixturePresentation().NativeStage!.Groups.Length);
        Assert.All(source.ReadFixturePresentation().NativeStage!.Groups, group =>
        {
            Assert.Equal(2, group.Source!.BoneCount);
            Assert.Equal(2, group.Matrices.Length);
        });
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([256])[255];
        Assert.Equal(0x979400u, binding.Entry);
        Assert.Equal(0x9793D0u, binding.Initializer);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(256);
        Assert.Equal(0x979A70u, profile.Callback);
        Assert.Equal(0x97E0A0u, profile.LoadCallback);
    }
}
