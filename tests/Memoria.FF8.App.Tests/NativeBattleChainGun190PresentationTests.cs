using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleChainGun190PresentationTests
{
    [Fact]
    public void Original_ChainGun190_presents_meshes_particles_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([190], "MEMORIA_FF8_CHAINGUN190", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)190, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 154 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 154);
        BattleEncounter formation = assets.LoadEncounter(155);
        Assert.Equal(40, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(8, caster.ModelId);
        Assert.Equal(255, caster.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -700), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("chain-gun190", effect, 8, 154, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHAINGUN190_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original ChainGun190 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([190])[189];
        Assert.Equal(NativeBattleChainGun190Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleChainGun190Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag149.tim", NativeBattleChainGun190Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(68128, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeChainGun190Resources(), binding.ChainGun190Resources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original ChainGun190 command8/action154/sequence14 and formation155; declared seven-HP post-formula presentation with logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
