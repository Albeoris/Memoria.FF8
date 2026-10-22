using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSaliva138PresentationTests
{
    [Fact]
    public void Original_Saliva_component_presents_original_ACTs_custom_mesh_and_native_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([138], "MEMORIA_FF8_SALIVA138", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)138, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 175 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 175);
        Assert.Equal((byte)2, action.AttackType);
        Assert.Equal((byte)48, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(512u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(378);
        Assert.Equal(98, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(44, caster.ModelId);
        Assert.Equal(new NativeBattleVector(-100, 0, -4500), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)16, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("saliva138", effect, 8, 175, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SALIVA138_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Saliva component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        BattleCombatantSnapshot caster = Assert.Single(source.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        BattleCombatantDefinition casterSource = Assert.Single(source.Definition.Combatants, actor => actor.Id == caster.Id);
        Assert.Equal(0, casterSource.SceneSlot);
        Assert.Equal(44, casterSource.ModelId);
        Assert.Equal((byte?)60, source.NativeGraphics!.Models[3].ModelId4);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([138])[137];
        Assert.Equal(NativeBattleSaliva138Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleSaliva138Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag137.tim", NativeBattleSaliva138Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(166432, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeSaliva138Resources(), binding.Saliva138Resources);
        Assert.True(source.NativeEffectMemory!.Saliva138!.PendingColdSources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command8/action175/sequence16 and formation378; declared seven-HP post-formula presentation with logical HP/status retained; no magic damage/status formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
