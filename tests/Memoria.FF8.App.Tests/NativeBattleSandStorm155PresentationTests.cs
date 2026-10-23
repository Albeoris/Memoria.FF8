using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSandStorm155PresentationTests
{
    [Fact]
    public void Original_Sand_Storm_component_presents_original_selected_mesh_curves_sprites_and_native_contacts()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([155], "MEMORIA_FF8_SAND_STORM155", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)155, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 39 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 39);
        BattleEncounter formation = assets.LoadEncounter(452);
        Assert.Equal(141, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(20, caster.ModelId);
        Assert.Equal(252, caster.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -3100), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("sand-storm155", effect, 8, 39, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SAND_STORM155_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Sand Storm component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        BattleCombatantSnapshot caster = Assert.Single(source.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        BattleCombatantDefinition casterSource = Assert.Single(source.Definition.Combatants, actor => actor.Id == caster.Id);
        Assert.Equal(0, casterSource.SceneSlot);
        Assert.Equal(20, casterSource.ModelId);
        Assert.Equal((byte?)36, source.NativeGraphics!.Models[3].ModelId4);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([155])[154];
        Assert.Equal(NativeBattleSandStorm155Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleSandStorm155Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag154.tim", NativeBattleSandStorm155Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(108064, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeSandStorm155Resources(), binding.SandStorm155Resources);
        Assert.NotNull(source.NativeEffectMemory!.SandStorm155!.RootBankStorage);
        Assert.NotNull(source.NativeEffectMemory.SandStorm155.ChildBankStorage);
        Assert.Equal(0u, source.NativeEffectMemory.SandStorm155.ReadDword(NativeBattleSandStorm155Profile.CursorGlobal));
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command8/action39/sequence14 and formation452; declared seven-HP post-formula presentation with logical HP/status retained; no magic damage/status formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
