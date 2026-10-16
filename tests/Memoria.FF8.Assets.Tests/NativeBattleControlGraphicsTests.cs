using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleControlGraphicsTests
{
    [Theory]
    [InlineData(0, false, 0x50BD00)]
    [InlineData(10, false, 0x50BD00)]
    [InlineData(0, true, 0x50BD80)]
    public void ControlHeadersCompleteTheirActualDescriptorWithoutReplayingGameplayAcrossRestore(byte sequence, bool contact, uint callback)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Enemy", ModelId = 61, SceneSlot = formation.Enemies.First(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = 94,
            StageId = formation.StageId,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeStageCameraBank = new([0, 0]),
            NativeStageCameraSelectors = [0, 0],
            NativeStageCameraControl = [2]
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
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        bool selected = false;
        bool waitedForSequence = false;
        for (int tick = 0; tick < 200 && battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104); tick++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            selected |= battle.NativeGraphics.ActionGraphics?.Callback == callback;
            waitedForSequence |= battle.NativeGraphics.Descriptors.Tasks.Any(task => task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 2);
        }
        Assert.True(selected);
        Assert.DoesNotContain(battle.NativeGraphics!.Descriptors.Queue, slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
        Assert.Equal(sequence == 10 && !contact, waitedForSequence);
        Assert.Equal(boundary.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(boundary.Random, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
    }
}
