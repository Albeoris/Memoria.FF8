using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNeedlesFamilyPresentationTests
{
    [Fact]
    public void Original_Needles_component_presents_declared_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([177, 178], "MEMORIA_FF8_NEEDLES_FAMILY", SelectOriginalFixture, PrepareDeclaredSource);
    }
    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Contains(effect, new ushort[] { 177, 178 });
        ushort actionIndex = effect == 177 ? (ushort)25 : (ushort)230;
        int model = effect == 177 ? 55 : 84;
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == actionIndex && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == actionIndex);
        Assert.Equal((byte)27, action.AttackType);
        Assert.Equal(effect == 177 ? (byte)10 : (byte)100, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(0u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(effect == 177 ? 394 : 712);
        Assert.Equal(effect == 177 ? 99 : 103, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(model, caster.ModelId);
        Assert.Equal(effect == 177 ? new NativeBattleVector(200, 0, -4500) : new NativeBattleVector(0, 0, -4000), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new(effect == 177 ? "thousand-needles177" : "ten-thousand-needles178", effect, 8, actionIndex, 0, 800, 300) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }
    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string prefix = "MEMORIA_FF8_NEEDLES_FAMILY";
        string output = Environment.GetEnvironmentVariable(prefix + "_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Needles component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        Assert.NotNull(game.ReadNativeEffectCatalogForEffects([request.Effect])[request.Effect - 1].NeedlesFamilyResources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit seven-HP post-formula presentation using the original enemy kernel row, sequence13 and formation; logical HP/status retained; no fixed1000/10000 damage formula, accuracy, ordinary AI turn or command admission", Contact = contact }));
        return source;
    }
}
