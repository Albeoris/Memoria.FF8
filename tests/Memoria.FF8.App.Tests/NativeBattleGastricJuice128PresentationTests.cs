using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGastricJuice128PresentationTests
{
    [Fact]
    public void Original_Gastric_Juice_component_presents_original_ACT_body_deformation_and_native_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([128], "MEMORIA_FF8_GASTRIC_JUICE128", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)128, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 77 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 77);
        Assert.Equal((byte)7, action.AttackType);
        Assert.Equal((byte)8, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(0x01000000u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(220);
        Assert.Equal(81, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(33, caster.ModelId);
        Assert.Equal(255, caster.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -2500), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("gastric-juice128", effect, 8, 77, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GASTRIC_JUICE128_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Gastric Juice component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        BattleCombatantSnapshot caster = Assert.Single(source.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        BattleCombatantDefinition casterSource = Assert.Single(source.Definition.Combatants, actor => actor.Id == caster.Id);
        Assert.Equal(0, casterSource.SceneSlot);
        Assert.Equal(33, casterSource.ModelId);
        Assert.Equal((byte?)49, source.NativeGraphics!.Models[3].ModelId4);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([128])[127];
        Assert.Equal(NativeBattleGastricJuice128Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleGastricJuice128Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag127.tim", NativeBattleGastricJuice128Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(166432, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeGastricJuice128Resources(), binding.GastricJuice128Resources);
        Assert.Equal(0u, source.NativeEffectMemory!.GastricJuice128!.ReadDword(NativeBattleGastricJuice128Profile.WorkingGlobal));
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command8/action77/sequence14 and formation220; declared seven-HP post-formula presentation with logical HP/status retained; no magic damage/status formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
