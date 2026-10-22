using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEnergyBomber217PresentationTests
{
    [Fact]
    public void Original_Energy_Bomber_component_preserves_both_enemies_and_presents_native_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([217], "MEMORIA_FF8_ENERGY_BOMBER217", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)217, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 316 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 316);
        Assert.Equal((byte)1, action.AttackType);
        Assert.Equal((byte)12, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(0u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(796);
        Assert.Equal(69, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new int[] { 0, 1 }, enemies.Select(enemy => enemy.Slot).ToArray());
        Assert.Equal(new int[] { 112, 113 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.All(enemies, enemy =>
        {
            Assert.Equal(new NativeBattleVector(0, 0, -2200), enemy.NativePosition);
            Assert.False(enemy.Hidden);
            Assert.False(enemy.InitiallySuspended);
            Assert.False(enemy.Untargetable);
        });
        Assert.Equal((byte)16, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(enemies[0].ModelId, enemies[0].Level), action));
        return new("energy-bomber217", effect, 8, 316, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENERGY_BOMBER217_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Energy Bomber component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        for (int index = 0; index < 2; index++)
        {
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, candidate => candidate.NativeParticipant!.ActorIndex == index + 3);
            BattleCombatantDefinition original = Assert.Single(source.Definition.Combatants, candidate => candidate.Id == actor.Id);
            Assert.Equal(index, original.SceneSlot);
            Assert.Equal(index + 112, original.ModelId);
            Assert.Equal((byte?)(index + 128), source.NativeGraphics!.Models[index + 3].ModelId4);
        }
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([217])[216];
        Assert.Equal(NativeBattleEnergyBomber217Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEnergyBomber217Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag216.tim", NativeBattleEnergyBomber217Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal("mag216.tim", binding.InitializerAsset!.Name);
        Assert.Equal(135712, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeEnergyBomber217Resources(), binding.EnergyBomber217Resources);
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command8/action316/sequence16 and both formation796 enemies; declared seven-HP post-formula contact with logical HP/status retained; no physical formula, accuracy, ordinary AI turn or command admission", Contact = contact }));
        return source;
    }
}
