using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBadBreath300PresentationTests
{
    [Fact]
    public void Original_Bad_Breath_component_presents_declared_immune_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([300], "MEMORIA_FF8_BAD_BREATH300", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)300, effect);
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == 10 && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == 10).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        ushort[] saved = [0x1A, 0x3A, 0x7E, 0x7F];
        uint[] battle = [0x4005, 0x500D, 0x500D, 0x100540D];
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal((byte)2, crisis[index].AttackType);
            Assert.Equal((byte)0, crisis[index].Power);
            Assert.Equal((byte?)255, crisis[index].Hit);
            Assert.Equal(saved[index], crisis[index].Statuses0);
            Assert.Equal(battle[index], crisis[index].Statuses1);
            Assert.Equal(40 + index, crisis[index].SupplementalKernelIndex);
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
        return new("bad-breath300-immune", effect, 15, 10, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 3, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = 1, ControlledContactValue = 0 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        Assert.Equal((byte)21, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BAD_BREATH300_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the Bad Breath component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        NativeBattleContactPacket contact = packets.ReadContact(0);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        NativeBattleContactPacket declared = contact with { Primary = contact.Primary with { HitFlags3 = 4 } };
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit declared Amount0 MISS presentation input with original command/action/sequence; actual HP/status retained; no status, accuracy, formula, ordinary command or AI admission", Before = contact, After = declared }));
        return source with { NativeActionPackets = packets with { Contacts = packets.Contacts.SetItem(0, declared) } };
    }
}
