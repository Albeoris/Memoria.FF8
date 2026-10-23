using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMorph172PresentationTests
{
    [Fact]
    public void Original_Morph172_presents_current_body_deformation_without_native_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([172], "MEMORIA_FF8_MORPH172", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)172, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 53 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 53);
        BattleEncounter formation = assets.LoadEncounter(124);
        Assert.Equal(35, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(29, caster.ModelId);
        Assert.Equal(255, caster.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -2500), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("morph172", effect, 8, 53, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MORPH172_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Morph172 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([172])[171];
        Assert.Equal(NativeBattleMorph172Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleMorph172Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag171.tim", NativeBattleMorph172Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(132640, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeMorph172Resources(), binding.Morph172Resources);
        BattleCombatantDefinition casterDefinition = source.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 0);
        NativeBattleGraphicsResource casterResource = assets.ReadNativeGraphicsResource(casterDefinition);
        NativeBattleGraphicsModel casterModel = source.NativeGraphics!.Models[3];
        Assert.Equal(29, casterDefinition.ModelId);
        Assert.Equal(41, casterResource.PoseSource!.BoneCount);
        Assert.Equal(41, NativeBattleBodyBoneMatrices.Require(casterModel, casterResource.PoseSource).Length);
        Assert.NotNull(casterModel.GeometryWorldMatrix);
        Assert.Equal(3, casterResource.Geometry!.Value.Length);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original Morph172 command8/action53/sequence13 and formation124; retained original prepared header with no synthesized contact callback; logical HP/status retained, no formula, accuracy or AI-turn admission", Contact = contact }));
        return source;
    }
}
