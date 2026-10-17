using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleOrdinaryGraphicsTests
{
    [Fact]
    public void Actual104OrdinaryServiceRunsIfritSequenceAndContactsWithoutReplayingGameplayAcrossRestore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        NativeBattleAiDefinition original = assets.ReadEnemyAi(61, 1);
        NativeBattleAiAbility ability = original.Abilities[3];
        BattleNativeAction action = assets.NativeActions.Single(action => action.Command == ability.Command && action.ActionId == ability.Id);
        Assert.Equal((byte)8, ability.Command);
        Assert.Equal((ushort)89, ability.Id);
        Assert.Equal((byte)14, ability.Sequence);
        Assert.Equal((ushort)0, action.MagicId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = formation.Enemies.First(enemy => enemy.ModelId == 61).Slot, Speed = 1, NativeAi = original with { Scripts = [[4, 200, 5, ability.Sequence, 7, checked((byte)ability.Id), 6, 0], [0], [0], [0], [0]] }, NativeCombatantFlags = 0x10 }
        ];
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, formation.Id);
        BattleDefinition definition = new()
        {
            Name = "Original104 service component",
            Combatants = actors,
            EncounterId = 94,
            StageId = formation.StageId,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeActions = [action],
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl,
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot ready = battle.Capture();
        for (int callback = 0; callback < 300 && !battle.Events.Any(item => item.Kind == "NativeText"); callback++)
        {
            battle.Tick();
        }
        Assert.Equal(0x50BB00u, battle.NativeGraphics!.ActionGraphics!.Callback);
        NativeBattleActionHeaderSlot header = battle.Capture().NativeActionPackets!.Headers[0];
        Assert.Equal(unchecked((byte)action.RawRecord[4]), header.ReadByte(3));
        Assert.Equal(ability.Sequence, header.ReadByte(2));
        Assert.Equal(action.MagicId, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal(original.Texts[3], Assert.Single(battle.Events, item => item.Kind == "NativeText").Message);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        int committedDamageEvents = battle.Events.Count(item => item.Kind == "Damage");
        for (int callback = 0; callback < 100 && (battle.ActiveAction is not null || battle.NativeGraphics.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104)); callback++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.DoesNotContain(battle.NativeGraphics.Descriptors.Queue, slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
        Assert.Equal(committedDamageEvents, battle.Events.Count(item => item.Kind == "Damage"));
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage" && item.Amount != 0);
        Assert.Equal(ready.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Null(battle.ActiveAction);
    }
}
