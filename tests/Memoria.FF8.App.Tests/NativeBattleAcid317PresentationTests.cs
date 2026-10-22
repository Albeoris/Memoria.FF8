using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAcid317PresentationTests
{
    [Fact]
    public void Original_Acid_component_presents_declared_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([317], "MEMORIA_FF8_ACID317", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)317, effect);
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == 7 && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == 7).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        byte[] powers = [30, 38, 44, 52];
        ushort[] statuses = [2, 10, 26, 30];
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal((byte)2, crisis[index].AttackType);
            Assert.Equal(powers[index], crisis[index].Power);
            Assert.Equal((byte?)255, crisis[index].Hit);
            Assert.Equal(statuses[index], crisis[index].Statuses0);
            Assert.Equal(index >= 2 ? 0x01000000u : 0u, crisis[index].Statuses1);
            Assert.Equal(28 + index, crisis[index].SupplementalKernelIndex);
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
        return new("acid317", effect, 15, 7, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 3, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = 1, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        Assert.Equal((byte)21, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACID317_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the Acid component.");
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
