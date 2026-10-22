using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleElectrocute275PresentationTests
{
    [Fact]
    public void Original_Electrocute_component_presents_declared_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([275], "MEMORIA_FF8_ELECTROCUTE275", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)275, effect);
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == 2 && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == 2).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        byte[] powers = [30, 36, 44, 50];
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal((byte)2, crisis[index].AttackType);
            Assert.Equal(powers[index], crisis[index].Power);
            Assert.Equal((byte?)255, crisis[index].Hit);
            Assert.Equal((ushort)0, crisis[index].Statuses0);
            Assert.Equal(0u, crisis[index].Statuses1);
            Assert.Equal(8 + index, crisis[index].SupplementalKernelIndex);
            Assert.Equal(3, crisis[index].CharacterId);
        }
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement target = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, target.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), target.NativePosition);
        Assert.False(target.Hidden);
        Assert.False(target.InitiallySuspended);
        Assert.False(target.Untargetable);
        return new("electrocute275", effect, 15, 2, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 3, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = 1, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        Assert.Equal((byte)21, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ELECTROCUTE275_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the Electrocute component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit declared seven-HP damage post-formula contact with original command/action/sequence and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission", Contact = contact }));
        return source;
    }
}
