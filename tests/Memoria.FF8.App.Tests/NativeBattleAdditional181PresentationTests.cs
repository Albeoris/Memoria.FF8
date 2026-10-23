using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAdditional181PresentationTests
{
    [Fact]
    public void Original_additional181_presents_current_body_meshes_particles_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([181], "MEMORIA_FF8_ADDITIONAL181", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)181, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 31 && row.ActionId == 8 && row.MagicId == effect && row.KernelSection == 10 && row.KernelIndex == 8);
        BattleEncounter formation = assets.LoadEncounter(94);
        Assert.Equal(31, formation.StageId);
        BattleEnemyPlacement target = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, target.Slot);
        Assert.Equal(61, target.ModelId);
        Assert.Equal(6, target.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -2100), target.NativePosition);
        Assert.False(target.Hidden);
        Assert.False(target.InitiallySuspended);
        Assert.False(target.Untargetable);
        return new("additional181", effect, 31, 8, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 0, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        Assert.Equal((byte)24, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ADDITIONAL181_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original additional181 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([181])[180];
        Assert.Equal(NativeBattleAdditional181Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleAdditional181Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag180.tim", NativeBattleAdditional181Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(165504, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeAdditional181Resources(), binding.Additional181Resources);
        BattleCombatantDefinition casterDefinition = source.Definition.Combatants.Single(actor => actor.IsPlayer);
        NativeBattleGraphicsResource casterResource = assets.ReadNativeGraphicsResource(casterDefinition);
        NativeBattleGraphicsModel casterModel = source.NativeGraphics!.Models[0];
        Assert.Equal(0, casterDefinition.ModelId);
        Assert.Equal((int?)1, casterModel.ActiveResourceRow100);
        Assert.Equal((int?)2, casterModel.LinkedTextureResourceIndex);
        Assert.Null(casterModel.ActiveStagedResourceRow100);
        Assert.Null(casterModel.LinkedStagedResourceRow);
        Assert.Equal((uint)0x1D997A8, NativeBattleModelResourceRow.Address100(casterModel));
        Assert.Equal(23, casterResource.PoseSource!.BoneCount);
        Assert.Equal(23, NativeBattleBodyBoneMatrices.Require(casterModel, casterResource.PoseSource).Length);
        Assert.Equal(2, casterResource.LinkedPoseSource!.BoneCount);
        Assert.Equal(2, casterModel.CurrentLinkedBoneMatrices!.Matrices.Length);
        Assert.NotNull(casterModel.GeometryWorldMatrix);
        Assert.Single(casterResource.Geometry!);
        Assert.Single(casterResource.LinkedGeometry!.Value);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command31/action8/sequence24 and formation94; declared seven-HP post-formula presentation with logical HP/status retained; no formula, status result, accuracy, ordinary command or card-conversion admission", Contact = contact }));
        return source;
    }
}
