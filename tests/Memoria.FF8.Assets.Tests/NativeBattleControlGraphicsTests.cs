using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleControlGraphicsTests
{
    [Theory]
    [InlineData(0, false, 0x50BD00)]
    [InlineData(10, false, 0x50BD00)]
    [InlineData(4, false, 0x50BD00)]
    [InlineData(0, true, 0x50BD80)]
    public void ControlHeadersCompleteTheirActualDescriptorWithoutReplayingGameplayAcrossRestore(byte sequence, bool contact, uint callback)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Enemy", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = formation.Enemies.First(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, formation.Id);
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = 94,
            StageId = formation.StageId,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeStageSource = assets.LoadStage(formation.StageId).NativeSource,
            NativeStageCameraBank = assets.LoadStage(formation.StageId).NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = assets.LoadStage(formation.StageId).NativeCameraControl
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int entry = 0; entry < 200 && !battle.NativeEntryReady; entry++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, int header) = packets.Prepare(new(0, 0, sequence, -1, 9, 0, null));
        if (contact)
        {
            (packets, _) = packets.AppendContact(new(new(3, 0, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(contact ? (byte)1 : (byte)0);
        BattleSnapshot boundary = battle.Capture();
        NativeBattleGraphicsState graphics = boundary.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        battle = BattleSession.Restore(boundary with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
        BattleSession restored = BattleSession.Restore(NativeBattleSnapshotJson.Deserialize<BattleSnapshot>(NativeBattleSnapshotJson.Serialize(battle.Capture()))!);
        bool selected = false;
        bool waitedForSequence = false;
        for (int tick = 0; tick < 200 && battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104); tick++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(NativeBattleSnapshotJson.Serialize(battle.Capture()), NativeBattleSnapshotJson.Serialize(restored.Capture()));
            selected |= battle.NativeGraphics.ActionGraphics?.Callback == callback;
            waitedForSequence |= battle.NativeGraphics.Descriptors.Tasks.Any(task => task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 2);
        }
        Assert.True(selected);
        Assert.DoesNotContain(battle.NativeGraphics!.Descriptors.Queue, slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
        Assert.Equal(sequence == 10 && !contact, waitedForSequence);
        if (sequence == 4)
        {
            //The controlled header selects the genuine installed attack sequence. Continue its callbacks
            //after50BD00 returns immediately; its originalB5/B6/B8 sound is not a descriptor-completion cue.
            for (int tick = 0; tick < 200 && !battle.Events.Any(item => item.NativeSound is not null); tick++)
            {
                battle.Tick();
                restored.Tick();
                Assert.Equal(NativeBattleSnapshotJson.Serialize(battle.Capture()), NativeBattleSnapshotJson.Serialize(restored.Capture()));
            }
            Assert.Contains(battle.Events, item => item.NativeSound is not null);
            BattleEvent sound = battle.Events.First(item => item.NativeSound is not null);
            NativeBattleModuleSound receipt = Assert.IsType<NativeBattleModuleSound>(sound.NativeModuleSound);
            Assert.Equal(receipt.Sound, battle.NativeGlobalSoundProcess!.OriginalEffectIds[receipt.ReadCommittedVoice()]);
        }
        Assert.Equal(boundary.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(boundary.Random, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
    }
}
