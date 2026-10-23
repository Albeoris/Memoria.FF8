using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect183PresentationTests
{
    [Fact]
    public void Original_effect183_presents_distinct_meshes_both_packet_banks_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([183], "MEMORIA_FF8_EFFECT183", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)183, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 23 && row.ActionId == 11 && row.MagicId == effect && row.KernelSection == 10 && row.KernelIndex == 11);
        Assert.Equal((byte)2, action.AttackType);
        Assert.Equal((byte)0, action.Power);
        Assert.Equal((byte)255, action.StatusAttack);
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
        return new("effect183", effect, 23, 11, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 0, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        Assert.Equal((byte)23, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT183_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original effect183 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([183])[182];
        Assert.Equal(NativeBattleEffect183Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEffect183Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag182.tim", NativeBattleEffect183Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(17440, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeEffect183Resources(), binding.Effect183Resources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command23/action11/sequence23 and formation94; declared seven-HP post-formula presentation with logical HP/status retained; no level formula, status result, accuracy or ordinary command admission", Contact = contact }));
        return source;
    }
}
