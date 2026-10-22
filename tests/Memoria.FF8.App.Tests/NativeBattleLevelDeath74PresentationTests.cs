using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleLevelDeath74PresentationTests
{
    [Fact]
    public void Original_Level_Death_component_presents_declared_immune_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([74], "MEMORIA_FF8_LEVEL_DEATH74", SelectOriginalFixture, PrepareImmuneSource);
    }

    [Fact]
    public void Original_Level_Death_nonimmune_component_presents_declared_contact_and_dark_bank_cold_boundary()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([74], "MEMORIA_FF8_LEVEL_DEATH74_ELIGIBLE", (assets, effect) => SelectOriginalFixture(assets, effect) with { Id = "level-death74-nonimmune" }, (game, assets, request) => PrepareDeclaredSource(game, assets, request, "MEMORIA_FF8_LEVEL_DEATH74_ELIGIBLE", 0));
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)74, effect);
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == 3 && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == 3).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal((byte)26, crisis[index].AttackType);
            Assert.Equal((ushort)1, crisis[index].Statuses0);
            Assert.Equal(0u, crisis[index].Statuses1);
            Assert.Equal((byte?)(4 - index), crisis[index].Hit);
            Assert.Equal(12 + index, crisis[index].SupplementalKernelIndex);
            Assert.Equal(3, crisis[index].CharacterId);
        }
        BattleNativeAction action = crisis[0];
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement target = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, target.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), target.NativePosition);
        Assert.False(target.Hidden);
        Assert.False(target.InitiallySuspended);
        Assert.False(target.Untargetable);
        return new("level-death74-immune", effect, action.Command, action.ActionId, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = action.CharacterId!.Value, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel, ControlledContactValue = 0 };
    }

    private static BattleSnapshot PrepareImmuneSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request) => PrepareDeclaredSource(game, assets, request, "MEMORIA_FF8_LEVEL_DEATH74", 4);

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request, string prefix, byte hitFlags)
    {
        Assert.Equal((byte)21, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable(prefix + "_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the Level Death component.");
        string fixture = Path.Combine(output, "fixture");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select the installed original resources."), fixture);
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        NativeBattleContactPacket contact = packets.ReadContact(0);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        NativeBattleContactPacket declared = contact with { Primary = contact.Primary with { HitFlags3 = hitFlags } };
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit declared post-formula presentation input; original command/action/sequence and physical target retained; no level comparison, KO/status result, formula, AI or ordinary command execution claim", DeclaredHitFlags = hitFlags, Before = contact, After = declared }));
        return source with { NativeActionPackets = packets with { Contacts = packets.Contacts.SetItem(0, declared) } };
    }
}
