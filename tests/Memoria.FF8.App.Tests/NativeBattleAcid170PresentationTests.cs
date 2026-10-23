using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAcid170PresentationTests
{
    [Fact]
    public void Original_Acid170_presents_actual_recipient_body_meshes_particles_and_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([170], "MEMORIA_FF8_ACID170", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)170, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 35 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 35);
        BattleEncounter formation = assets.LoadEncounter(71);
        Assert.Equal(15, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(36, caster.ModelId);
        Assert.Equal(255, caster.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -1900), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("acid170", effect, 8, 35, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACID170_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Acid170 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([170])[169];
        Assert.Equal(NativeBattleAcid170Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleAcid170Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag169.tim", NativeBattleAcid170Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(165408, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeAcid170Resources(), binding.Acid170Resources);
        BattleCombatantDefinition casterDefinition = source.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 0);
        NativeBattleGraphicsResource casterResource = assets.ReadNativeGraphicsResource(casterDefinition);
        NativeBattleGraphicsModel casterModel = source.NativeGraphics!.Models[3];
        Assert.Equal(36, casterDefinition.ModelId);
        Assert.Equal(42, casterResource.PoseSource!.BoneCount);
        Assert.Equal(42, NativeBattleBodyBoneMatrices.Require(casterModel, casterResource.PoseSource).Length);
        Assert.NotNull(casterModel.GeometryWorldMatrix);
        Assert.Single(casterResource.Geometry!);
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
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original Acid170 command8/action35/sequence15 and formation71; declared seven-HP post-formula presentation with logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
