using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBeamLaser134PresentationTests
{
    [Fact]
    public void Original_Beam_Laser_component_presents_declared_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([134], "MEMORIA_FF8_BEAM_LASER134", SelectOriginalFixture, PrepareDeclaredSource);
    }
    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)134, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 176 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 176);
        Assert.Equal((byte)1, action.AttackType);
        Assert.Equal((byte)21, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(0u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(227);
        Assert.Equal(71, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(26, caster.ModelId);
        Assert.Equal(new NativeBattleVector(60, 0, -2790), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("beam-laser134", effect, 8, 176, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }
    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BEAM_LASER134_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Beam Laser component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        Assert.NotNull(game.ReadNativeEffectCatalogForEffects([134])[133].BeamLaser134Resources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit seven-HP post-formula presentation using original command8/action176/sequence14 and formation227; logical HP/status retained; no damage formula, accuracy, ordinary AI turn or command admission", Contact = contact }));
        return source;
    }
}
