using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBladeShot12PresentationTests
{
    [Fact]
    public void Original_BladeShot12_presents_object7_roundtrip_morphs_children_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([12], "MEMORIA_FF8_BLADESHOT12", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)12, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 7 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 7);
        BattleEncounter formation = assets.LoadEncounter(218);
        Assert.Equal(80, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(9, caster.ModelId);
        Assert.Equal(255, caster.Level);
        Assert.Equal(new NativeBattleVector(300, 0, -2800), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("blade-shot12", effect, 8, 7, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BLADESHOT12_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original BladeShot12 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([12])[11];
        Assert.Equal(NativeBattleBladeShot12Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleBladeShot12Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag011.tim", NativeBattleBladeShot12Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(167968, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeBladeShot12Resources(), binding.BladeShot12Resources);
        BattleCombatantDefinition casterDefinition = source.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 0);
        NativeBattleGraphicsResource casterResource = assets.ReadNativeGraphicsResource(casterDefinition);
        NativeBattleGraphicsModel casterModel = source.NativeGraphics!.Models[3];
        Assert.Equal(9, casterDefinition.ModelId);
        Assert.Equal(31, casterResource.PoseSource!.BoneCount);
        Assert.Equal(31, NativeBattleBodyBoneMatrices.Require(casterModel, casterResource.PoseSource).Length);
        Assert.NotNull(casterModel.GeometryWorldMatrix);
        Assert.Equal(8, casterResource.Geometry!.Value.Length);
        NativeBattleRawMesh object7 = casterResource.Geometry!.Value[7];
        Assert.Equal(34, object7.Vertices.Length);
        Assert.Equal(16, object7.Polygons.Count(face => !face.Quad));
        Assert.Equal(24, object7.Polygons.Count(face => face.Quad));
        Assert.NotNull(casterModel.SequenceState);
        Assert.Null(casterModel.ActiveStagedResourceRow100);
        Assert.NotNull(casterModel.ActiveResourceRow100);
        Assert.InRange(casterModel.ActiveResourceRow100!.Value, 0, 10);
        BattleCombatantDefinition recipientDefinition = source.Definition.Combatants.Single(actor => actor.IsPlayer);
        NativeBattleGraphicsResource recipientResource = assets.ReadNativeGraphicsResource(recipientDefinition);
        NativeBattleGraphicsModel recipientModel = source.NativeGraphics.Models[0];
        Assert.Equal(0, recipientDefinition.ModelId);
        Assert.Equal((int?)1, recipientModel.ActiveResourceRow100);
        Assert.Equal((int?)2, recipientModel.LinkedTextureResourceIndex);
        Assert.Null(recipientModel.ActiveStagedResourceRow100);
        Assert.Null(recipientModel.LinkedStagedResourceRow);
        Assert.Equal((uint)0x1D997A8, NativeBattleModelResourceRow.Address100(recipientModel));
        Assert.Equal(23, recipientResource.PoseSource!.BoneCount);
        Assert.Equal(23, NativeBattleBodyBoneMatrices.Require(recipientModel, recipientResource.PoseSource).Length);
        Assert.Equal(2, recipientResource.LinkedPoseSource!.BoneCount);
        Assert.Equal(2, recipientModel.CurrentLinkedBoneMatrices!.Matrices.Length);
        Assert.NotNull(recipientModel.GeometryWorldMatrix);
        Assert.Single(recipientResource.Geometry!);
        Assert.Single(recipientResource.LinkedGeometry!.Value);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original Blade Shot12 command8/action7/sequence15 and formation218; actual model9 object7 mesh-mask80 owner and current caster31/recipient23+linked2 bone owners, declared first physical seven-HP presentation with logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission", CasterMeshMask124 = casterModel.SequenceState!.MeshMask124, Object7Vertices = object7.Vertices.Length, Contact = contact }));
        return source;
    }
}
