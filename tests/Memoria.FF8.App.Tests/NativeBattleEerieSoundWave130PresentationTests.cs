using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEerieSoundWave130PresentationTests
{
    [Fact]
    public void Original_Eerie_Sound_Wave_component_presents_declared_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([130], "MEMORIA_FF8_EERIE_SOUND_WAVE130", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)130, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 185 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 185);
        Assert.Equal((byte)2, action.AttackType);
        Assert.Equal((byte)22, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(0u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(376);
        Assert.Equal(127, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(48, caster.ModelId);
        Assert.Equal(new NativeBattleVector(-100, 0, -4200), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("eerie-sound-wave130", effect, 8, 185, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EERIE_SOUND_WAVE130_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the Eerie Sound Wave component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        Assert.NotNull(game.ReadNativeEffectCatalogForEffects([130])[129].EerieSoundWave130Resources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit seven-HP post-formula presentation using original enemy command8/action185/sequence13 and formation376, with retained logical HP/status; no damage/status formula, accuracy, ordinary AI turn or command admission", Contact = contact }));
        return source;
    }
}
