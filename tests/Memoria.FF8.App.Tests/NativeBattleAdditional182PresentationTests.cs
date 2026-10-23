using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAdditional182PresentationTests
{
    [Fact]
    public void Original_Additional182_clear_contact_branch_presents_original_camera_history_and_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([182], "MEMORIA_FF8_ADDITIONAL182_CLEAR", (assets, effect) => SelectOriginalFixture(assets, effect, "additional182-clear"), (game, assets, request) => PrepareDeclaredSource(game, assets, request, "MEMORIA_FF8_ADDITIONAL182_CLEAR", 0));
    }

    [Fact]
    public void Original_Additional182_set_contact_branch_presents_original_camera_particles_and_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([182], "MEMORIA_FF8_ADDITIONAL182_SET", (assets, effect) => SelectOriginalFixture(assets, effect, "additional182-set"), (game, assets, request) => PrepareDeclaredSource(game, assets, request, "MEMORIA_FF8_ADDITIONAL182_SET", 4));
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect, string id)
    {
        Assert.Equal((ushort)182, effect);
        Assert.Single(assets.NativeActions, row => row.Command == 29 && row.ActionId == 10 && row.MagicId == effect && row.KernelSection == 10 && row.KernelIndex == 10);
        BattleEncounter formation = assets.LoadEncounter(94);
        Assert.Equal(31, formation.StageId);
        BattleEnemyPlacement target = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, target.Slot);
        Assert.Equal(61, target.ModelId);
        Assert.Equal(6, target.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -2100), target.NativePosition);
        Assert.False(target.Hidden);
        Assert.False(target.InitiallySuspended);
        Assert.False(target.Untargetable);
        return new(id, effect, 29, 10, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 0, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, ControlledContactValue = 0 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request, string prefix, byte hitFlags)
    {
        Assert.Equal((byte)11, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable(prefix + "_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Additional182 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([182])[181];
        Assert.Equal(NativeBattleAdditional182Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleAdditional182Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag181.tim", NativeBattleAdditional182Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(165504, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeAdditional182Resources(), binding.Additional182Resources);
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        NativeBattleContactPacket contact = packets.ReadContact(0);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        NativeBattleContactPacket declared = contact with { Primary = contact.Primary with { HitFlags3 = hitFlags } };
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command29/action10/sequence11 and formation94; declared Amount0 post-formula presentation contact with logical HP/status retained; contact BYTE3 bit4 selects the native director/camera/sound only, without formula, accuracy, status, ordinary command, AI or card-conversion admission", DeclaredHitFlags = hitFlags, Before = contact, After = declared }));
        return source with { NativeActionPackets = packets with { Contacts = packets.Contacts.SetItem(0, declared) } };
    }
}
