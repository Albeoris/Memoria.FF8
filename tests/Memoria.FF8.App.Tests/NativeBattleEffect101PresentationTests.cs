using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect101PresentationTests
{
    [Fact]
    public void Original_unnamed_component_restores_target_without_a_sequence_contact_event()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([101], "MEMORIA_FF8_EFFECT101", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)101, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 290 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 290);
        Assert.Equal((byte)0, action.AttackType);
        Assert.Equal((byte)0, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(0u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(431);
        Assert.Equal(126, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(1, caster.Slot);
        Assert.Equal(109, caster.ModelId);
        Assert.Equal(new NativeBattleVector(0, 0, -4600), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("effect101", effect, 8, 290, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT101_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Effect101 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        BattleCombatantSnapshot caster = Assert.Single(source.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        BattleCombatantDefinition casterSource = Assert.Single(source.Definition.Combatants, actor => actor.Id == caster.Id);
        Assert.Equal(1, casterSource.SceneSlot);
        Assert.Equal(109, casterSource.ModelId);
        Assert.Equal((byte?)125, source.NativeGraphics!.Models[3].ModelId4);
        Assert.Null(source.NativeGraphics.Models[4].ModelId4);
        Assert.NotNull(game.ReadNativeEffectCatalogForEffects([101])[100].Effect101Resources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command8/action290/caster sequence13, actual recipient sequence14 has no contact event; declared seven-HP packet remains unused, logical HP/status retained; no formula, accuracy, ordinary AI turn or command admission", ExpectedContacts = 0, ExpectedGlyphs = 0, Contact = contact }));
        return source;
    }
}
