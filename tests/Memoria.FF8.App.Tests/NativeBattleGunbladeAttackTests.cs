using System.Text.Json;
using System.Reflection;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGunbladeAttackTests
{
    [Fact]
    public void Named_visible_shot_standalone_continues_one_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUNBLADE_VISIBLE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "Select a standalone visible gunblade shot.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        Assert.Contains(snapshot.NativeGraphics!.ModuleDraws, draw => draw.Kind == NativeBattleModuleDrawKind.Sprite && draw.Resource?.Id is 13 or 16);
        BattleSession battle = BattleSession.Restore(snapshot);
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, snapshot.Definition, true).Render(clone, 320, 224));
        battle.Tick();
        clone.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
        Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, snapshot.Definition, true).Render(clone, 320, 224));
    }

    [Fact]
    public void Named_pending_shot_standalone_continues_one_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUNBLADE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "Select a standalone pending gunblade shot.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        Assert.NotNull(snapshot.NativeGraphics!.PendingTriggerSoundActorIndex);
        BattleSession battle = BattleSession.Restore(snapshot);
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        int before = battle.Events.Count;
        battle.Tick();
        clone.Tick();
        Assert.Null(battle.NativeGraphics!.PendingTriggerSoundActorIndex);
        Assert.Single(battle.Events.Skip(before), item => item.Kind == "NativeModuleSound" && item.ActorId == "party");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
    }

    [Theory]
    [InlineData(0, BattleGunbladeMode.Automatic, false)]
    [InlineData(6, BattleGunbladeMode.Automatic, false)]
    [InlineData(0, BattleGunbladeMode.Manual, false)]
    [InlineData(6, BattleGunbladeMode.Manual, false)]
    [InlineData(0, BattleGunbladeMode.Manual, true)]
    [InlineData(6, BattleGunbladeMode.Manual, true)]
    public void Trigger_reaches_original_contact_and_shot_sound_once_after_JSON_restore(int character, BattleGunbladeMode mode, bool early)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, character, 116);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.SetGunbladeMode(mode);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("party"));
        battle.QueueAttack("party", "enemy");
        bool restoredWindow = false;
        bool pressed = false;
        bool restoredSound = false;
        bool checkedContact = false;
        bool visibleParticle = false;
        bool sawFlash = false;
        bool continuedVisual = false;
        BattleSession? visualClone = null;
        BattleScenePresentation scene = new(assets, definition, true);
        byte particleId = checked((byte)(character == 0 ? early ? 12 : 13 : early ? 15 : 16));
        List<NativeBattleModuleSound> sounds = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: sounds.Add, stopSounds: () => { });
        audio.Update(battle);
        List<int> distances = [];
        for (int callback = 0; callback < 400 && !battle.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == "party"); callback++)
        {
            if (battle.NativeGraphics!.TriggerWidget is { ClosingPhase: 0 } widget)
            {
                distances.Add(widget.Input.PoseClock + widget.Input.SubframeClock - widget.Block.Timeline.InputThresholds[widget.Input.ContactIndex]);
                if (mode == BattleGunbladeMode.Manual && !pressed && (early || battle.GunbladeTriggerWindowOpen))
                {
                    Assert.Equal(!early, battle.TriggerGunblade());
                    pressed = true;
                    if (!early)
                    {
                        //Isolate the actual widget service before its next graphical sequence tail.
                        typeof(BattleSession).GetMethod("DispatchNativeUiService", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);
                    }
                }
                if (!restoredWindow && widget.Input.Phase == 6)
                {
                    BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                    battle.Tick();
                    clone.Tick();
                    audio.Update(battle);
                    ObservePresentation();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    restoredWindow = true;
                    continue;
                }
            }
            if (!restoredSound && battle.NativeGraphics.PendingTriggerSoundActorIndex is byte owner)
            {
                Assert.Equal((byte)0, owner);
                BattleSnapshot snapshot = battle.Capture();
                string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUNBLADE_OUTPUT");
                if (output is not null)
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output, $"gunblade-{character}-{mode}-pending-sound.battle.json"), JsonSerializer.Serialize(snapshot));
                }
                BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
                battle.Tick();
                clone.Tick();
                audio.Update(battle);
                ObservePresentation();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                Assert.Null(battle.NativeGraphics.PendingTriggerSoundActorIndex);
                restoredSound = true;
                continue;
            }
            battle.Tick();
            audio.Update(battle);
            ObservePresentation();
            if (!checkedContact && battle.Events.Any(item => item.Kind == "Damage" && item.ActorId == "party"))
            {
                NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(0).Primary;
                Assert.Equal(early ? 0 : 2, contact.HitFlags3 & 2);
                Assert.Equal(early ? (byte)4 : (byte)6, contact.Sequence);
                checkedContact = true;
            }
        }
        if (visualClone is not null)
        {
            battle.Tick();
            audio.Update(battle);
            ObservePresentation();
        }
        Assert.True(restoredWindow);
        Assert.True(checkedContact);
        Assert.True(visibleParticle, $"Original shot particle resource{particleId} did not change any rendered RGB pixels.");
        Assert.True(continuedVisual);
        Assert.Equal(!early, sawFlash);
        Assert.Contains(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "party");
        Assert.True(battle.Events.Count(item => item.Kind == "GunbladeTrigger") == (early ? 0 : 1), "Observed widget distances: " + string.Join(",", distances));
        Assert.Equal(mode == BattleGunbladeMode.Manual && !early, restoredSound);
        uint shot = definition.NativeGraphics!.Rules.ActorSounds1120!.Value[character * 7 + (character == 0 ? 1 : 0)];
        Assert.Equal(early ? 0 : 1, sounds.Count(sound => sound.Sound == shot && sound.Flags == 0 && sound.Anchor is not null));
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.ActorId == "party");

        void ObservePresentation()
        {
            if (visualClone is not null)
            {
                visualClone.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(visualClone.Capture()));
                Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(visualClone, 320, 224));
                visualClone = null;
                continuedVisual = true;
            }
            NativeBattleGraphicsState graphics = battle.NativeGraphics!;
            sawFlash |= graphics.ModuleDraws.Any(draw => draw.Kind == NativeBattleModuleDrawKind.Flash);
            if (visibleParticle)
            {
                return;
            }
            HashSet<int> draws = graphics.ModuleDraws.Select((draw, index) => (draw, index)).Where(pair => pair.draw.Kind == NativeBattleModuleDrawKind.Sprite && pair.draw.Resource?.Id == particleId).Select(pair => pair.index).ToHashSet();
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
            if (!frame.LastCommands.Any(command => command.Command.ModuleDraw is int index && draws.Contains(index)))
            {
                return;
            }
            BattleSnapshot snapshot = battle.Capture();
            byte[] pixels = scene.Render(battle, 320, 224);
            NativeBattlePrimitiveFrame withoutParticle = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.ModuleDraw is not int index || !draws.Contains(index)).ToImmutableArray() };
            BattleSession plain = BattleSession.Restore(snapshot with { NativeGraphics = graphics with { PrimitiveFrame = withoutParticle } });
            byte[] background = new BattleScenePresentation(assets, definition, true).Render(plain, 320, 224);
            int changed = 0;
            for (int offset = 0; offset < pixels.Length; offset += 4)
            {
                if (!pixels.AsSpan(offset, 3).SequenceEqual(background.AsSpan(offset, 3)))
                {
                    changed++;
                }
            }
            if (changed == 0)
            {
                return;
            }
            visibleParticle = true;
            visualClone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUNBLADE_OUTPUT");
            string name = $"gunblade-{character}-{mode}-early-{early}-visible";
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, pixels);
            if (output is not null)
            {
                File.WriteAllText(Path.Combine(output, name + "-result.json"), JsonSerializer.Serialize(new { Resource = particleId, ChangedRgbPixels = changed, snapshot.Frame, graphics.Callback }));
            }
        }
    }
}
