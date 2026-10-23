using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBreath129PresentationTests
{
    [Fact]
    public void Original_Breath129_presents_current_mesh_deformation_body_capture_fibres_curves_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([129], "MEMORIA_FF8_BREATH129", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)129, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 102 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 102);
        BattleEncounter formation = assets.LoadEncounter(338);
        Assert.Equal(88, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(53, caster.ModelId);
        Assert.Equal(215, caster.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -2300), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("breath129", effect, 8, 102, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREATH129_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Breath129 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([129])[128];
        Assert.Equal(NativeBattleBreath129Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleBreath129Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag128.tim", NativeBattleBreath129Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(166944, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeBreath129Resources(), binding.Breath129Resources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original Breath129 command8/action102/sequence12 and formation338; declared seven-HP post-formula presentation with logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
