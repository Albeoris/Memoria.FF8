using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOilShot136PresentationTests
{
    [Fact]
    public void Original_Oil_Shot_component_presents_original_ACTs_custom_mesh_and_native_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([136], "MEMORIA_FF8_OIL_SHOT136", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)136, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 180 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 180);
        Assert.Equal((byte)1, action.AttackType);
        Assert.Equal((byte)30, action.Power);
        Assert.Equal((ushort)8, action.Statuses0);
        Assert.Equal(0u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(384);
        Assert.Equal(98, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(99, caster.ModelId);
        Assert.Equal(new NativeBattleVector(0, 0, -3400), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("oil-shot136", effect, 8, 180, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OIL_SHOT136_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Oil Shot component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        BattleCombatantSnapshot caster = Assert.Single(source.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        BattleCombatantDefinition casterSource = Assert.Single(source.Definition.Combatants, actor => actor.Id == caster.Id);
        Assert.Equal(0, casterSource.SceneSlot);
        Assert.Equal(99, casterSource.ModelId);
        Assert.Equal((byte?)115, source.NativeGraphics!.Models[3].ModelId4);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([136])[135];
        Assert.Equal(NativeBattleOilShot136Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleOilShot136Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag135.tim", NativeBattleOilShot136Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(134176, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeOilShot136Resources(), binding.OilShot136Resources);
        Assert.True(source.NativeEffectMemory!.OilShot136!.PendingColdSources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command8/action180/sequence13 and formation384; declared seven-HP post-formula presentation with logical HP/status retained; no magic damage/status formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
