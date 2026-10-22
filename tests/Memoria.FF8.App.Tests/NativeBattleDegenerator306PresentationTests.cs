using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDegenerator306PresentationTests
{
    [Fact]
    public void Original_Degenerator_nonimmune_component_presents_declared_contact_and_deformed_body()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([306], "MEMORIA_FF8_DEGENERATOR306", (assets, effect) => SelectOriginalFixture(assets, effect, "degenerator306-nonimmune"), (game, assets, request) => PrepareDeclaredSource(game, assets, request, "MEMORIA_FF8_DEGENERATOR306", 0));
    }

    [Fact]
    public void Original_Degenerator_immune_component_presents_declared_contact_and_skips_deformed_body()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([306], "MEMORIA_FF8_DEGENERATOR306_IMMUNE", (assets, effect) => SelectOriginalFixture(assets, effect, "degenerator306-immune"), (game, assets, request) => PrepareDeclaredSource(game, assets, request, "MEMORIA_FF8_DEGENERATOR306_IMMUNE", 4));
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect, string id)
    {
        Assert.Equal((ushort)306, effect);
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == 4 && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == 4).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal((byte)2, crisis[index].AttackType);
            Assert.Equal((byte)0, crisis[index].Power);
            Assert.Equal((byte?)255, crisis[index].Hit);
            Assert.Equal((ushort)0, crisis[index].Statuses0);
            Assert.Equal(0x10000u, crisis[index].Statuses1);
            Assert.Equal(16 + index, crisis[index].SupplementalKernelIndex);
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
        return new(id, effect, 15, 4, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 3, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = 1, ControlledContactValue = 0 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request, string prefix, byte hitFlags)
    {
        Assert.Equal((byte)21, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable(prefix + "_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the Degenerator component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        NativeBattleContactPacket contact = packets.ReadContact(0);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        NativeBattleContactPacket declared = contact with { Primary = contact.Primary with { HitFlags3 = hitFlags } };
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit declared Amount0 presentation contact with original command/action/sequence and retained logical HP/status; non-MISS and MISS select native body/visibility branches only, without status, removal-formula, accuracy, ordinary command or AI admission", DeclaredHitFlags = hitFlags, Before = contact, After = declared }));
        return source with { NativeActionPackets = packets with { Contacts = packets.Contacts.SetItem(0, declared) } };
    }
}
