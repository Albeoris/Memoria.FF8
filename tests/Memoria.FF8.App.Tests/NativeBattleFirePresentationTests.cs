using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFirePresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Fire_runs_original_service_camera_sprites_contact_and_sound_through_mid_effect_restore(bool casting)
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed original Fire resources are required.");
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
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
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalog(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        if (casting)
        {
            NativeBattleGraphicsResource resource = definition.NativeGraphics!.Bindings[0].Resource;
            Assert.Equal(Convert.FromHexString("C308D40002E57FC100CB7FE90DC308DC0002E508E607E602A1E6E5"), resource.SequenceSection.AsSpan().Slice(resource.SequenceStart(11) + 0x38, 27).ToArray());
        }
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot before = battle.Capture();
        if (!casting)
        {
            AssertNativeFileIoHostPump(before);
        }
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, int header) = packets.Prepare(new(0, casting ? (byte)2 : (byte)252, casting ? (byte)11 : (byte)0, -1, 1, 2, null));
        (packets, _) = packets.AppendContact(new(new(3, 5, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        battle = BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
        BattleScenePresentation scene = new(assets, definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(() => { }, _ => { }, _ => { }, playModule: heard.Add);
        audio.Update(battle);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EFFECT_EVIDENCE");
        string evidenceId = (casting ? "fire-casting-" : "fire-service-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        PublishEvidence(evidence, evidenceId + "-before", battle, scene.Render(battle, 320, 224));
        BattleSession? restored = null;
        bool selected = false;
        bool sawSprites = false;
        bool sawPixelDifference = false;
        bool checkedNextFrame = false;
        NativeBattleCameraPose? initialCamera = battle.NativeCamera?.CurrentPose;
        bool cameraProgramCompleted = false;
        bool sawCastingLoadWait = false;
        bool sawCastingReady = false;
        bool sawCastingCompletionConsumed = false;
        int peakDifferencePixels = 0;
        BattleSnapshot? peakSnapshot = null;
        byte[]? peakVisible = null;
        byte[]? peakPlain = null;
        for (int callback = 0; callback < 360 && HasActionDescriptor(battle); callback++)
        {
            bool castingCompletionPending = casting && battle.NativeGraphics!.Descriptors.Tasks.Any(task => task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 7);
            battle.AdvanceNativeGraphicsPresentation(Callback);
            if (castingCompletionPending && !HasActionDescriptor(battle))
            {
                NativeBattleGraphicsModel actor = battle.NativeGraphics!.Models[0];
                //50A9A0 sets WORD44 bit200 before504290; original sequence11 XOR-clears it and selects pose22 in the same callback.
                Assert.Equal((byte)11, actor.Sequence);
                Assert.Equal((ushort)0x54, actor.SequenceState!.Offset46);
                Assert.Equal((byte)22, actor.SequenceState.Pose);
                Assert.Equal(0, actor.ResourceFlags44 & 0x200);
                sawCastingCompletionConsumed = true;
            }
            audio.Update(battle);
            audio.Update(battle);
            selected |= battle.NativeGraphics!.ActionGraphics?.Callback == (casting ? 0x50A9A0 : 0x50B190);
            cameraProgramCompleted |= battle.NativeCamera is { ControlCursor: null, ControlResource.Source.Identity: "FF8_EN:DED5FC" };
            sawCastingLoadWait |= battle.NativeGraphics.Descriptors.Tasks.Any(task => task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 3);
            sawCastingReady |= battle.NativeGraphics.Descriptors.Tasks.Any(task => task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 4) && (battle.NativeGraphics.Models[0].ResourceFlags44 & 0x20) != 0;
            if (restored is not null)
            {
                restored.AdvanceNativeGraphicsPresentation(Callback);
                Assert.Equal(battle.Capture().NativeCrtRandom, restored.Capture().NativeCrtRandom);
                if (!checkedNextFrame)
                {
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(restored, 320, 224));
                    checkedNextFrame = true;
                }
            }
            if (!battle.NativeGraphics.ModuleDraws.Any(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite))
            {
                continue;
            }
            sawSprites = true;
            BattleSnapshot snapshot = battle.Capture();
            byte[] visible;
            try
            {
                visible = scene.Render(battle, 320, 224).ToArray();
            }
            catch (NotSupportedException exception)
            {
                if (!string.IsNullOrWhiteSpace(evidence))
                {
                    Directory.CreateDirectory(evidence);
                    string name = evidenceId + "-unsupported-callback" + snapshot.NativeGraphics!.Callback;
                    File.WriteAllText(Path.Combine(evidence, name + ".battle.json"), JsonSerializer.Serialize(snapshot));
                    File.WriteAllText(Path.Combine(evidence, name + "-details.json"), JsonSerializer.Serialize(new { exception.Message, snapshot.Frame, snapshot.NativeGraphics.Callback, Meshes = snapshot.NativeGraphics.ModuleDraws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Mesh).Select(draw => new { draw.MeshId, draw.MeshFlags }) }));
                }
                throw;
            }
            NativeBattleGraphicsState currentGraphics = snapshot.NativeGraphics!;
            NativeBattlePrimitiveFrame currentFrame = currentGraphics.PrimitiveFrame ?? throw new InvalidDataException("Native Fire pixel comparison requires its actual executed primitive frame.");
            HashSet<int> spriteDraws = currentGraphics.ModuleDraws.Select((draw, index) => (draw, index)).Where(pair => pair.draw.Kind == NativeBattleModuleDrawKind.Sprite).Select(pair => pair.index).ToHashSet();
            NativeBattlePrimitiveFrame withoutSpritePackets = currentFrame with { LastCommands = currentFrame.LastCommands.Where(command => command.Command.ModuleDraw is not int drawIndex || !spriteDraws.Contains(drawIndex)).ToImmutableArray() };
            BattleSession withoutSprites = BattleSession.Restore(snapshot with { NativeGraphics = currentGraphics with { PrimitiveFrame = withoutSpritePackets } });
            byte[] plain = scene.Render(withoutSprites, 320, 224).ToArray();
            int changedPixels = DifferentPixels(visible, plain);
            if (changedPixels > peakDifferencePixels)
            {
                peakDifferencePixels = changedPixels;
                peakSnapshot = snapshot;
                peakVisible = visible;
                peakPlain = plain;
            }
            if (sawPixelDifference || changedPixels == 0)
            {
                continue;
            }
            sawPixelDifference = true;
            string json = JsonSerializer.Serialize(snapshot);
            restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
            Assert.Equal(visible, new BattleScenePresentation(assets, definition, true).Render(restored, 320, 224));
            Assert.Equal(json, JsonSerializer.Serialize(battle.Capture()));
            PublishEvidence(evidence, evidenceId + "-mid", battle, visible);
            PublishPixels(evidence, evidenceId + "-mid-without-sprites", plain);
        }
        Assert.True(selected, "Native104 did not select its original effect/casting module service.");
        Assert.True(sawSprites, "Original Fire callbacks emitted no typed sprite draw.");
        Assert.True(sawPixelDifference, "Actual Fire sprite commands did not change the rendered pixels.");
        Assert.True(peakDifferencePixels >= 1024, $"The peak original Fire frame changed only {peakDifferencePixels} pixels.");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            PublishEvidence(evidence, evidenceId + "-peak", BattleSession.Restore(peakSnapshot!), peakVisible!);
            PublishPixels(evidence, evidenceId + "-peak-without-sprites", peakPlain!);
            File.WriteAllText(Path.Combine(evidence, evidenceId + "-peak-details.json"), JsonSerializer.Serialize(new { ChangedPixels = peakDifferencePixels, Callback = peakSnapshot!.NativeGraphics!.Callback, SpriteCount = peakSnapshot.NativeGraphics.ModuleDraws.Count(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite) }));
        }
        Assert.True(checkedNextFrame, "No visible mid-effect snapshot made a subsequent continuation check.");
        Assert.True(cameraProgramCompleted, "Actual DED5FC camera control program did not register and finish.");
        if (!casting)
        {
            //50B190 sets selectorFFFF. DED5FC computes0-selector; its <=0 branch is false and opcode02 retains the prior pose.
            Assert.Equal((short)-1, battle.NativeCamera!.Variables[0]);
            Assert.Equal((short)-1, battle.NativeCamera.Scratch[0]);
            Assert.Equal(initialCamera, battle.NativeCamera.CurrentPose);
        }
        else
        {
            Assert.True(sawCastingLoadWait, "Original casting service did not wait in its load/camera phase3.");
            Assert.True(sawCastingReady, "Original model casting sequence did not acknowledge resourceWORD44 with bit20 in phase4.");
            Assert.True(sawCastingCompletionConsumed, "Original sequence11 did not consume the50A9A0 completion signal in its native model callback.");
        }
        Assert.False(HasActionDescriptor(battle), "Original Fire104 descriptor did not finish within360 callbacks.");
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal(before.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(before.Random, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
        NativeBattleModuleSound sound = Assert.Single(heard, sound => sound.Sound == 300000);
        Assert.Equal((ushort)0x100, sound.Flags);
        Assert.NotNull(sound.Anchor);
        Assert.NotNull(sound.Pan);
        Assert.Equal(9, FieldAudioPresentation.ResolveNativeModuleSoundVoice(sound, -10));
        Assert.Equal((uint)sound.Pan!.Value >> 1, FieldAudioPresentation.ResolveNativeModuleSoundPan(sound));
        Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == 300000);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored!.Capture()));
        int soundsBeforeRestore = heard.Count;
        audio.Update(BattleSession.Restore(battle.Capture()));
        Assert.Equal(soundsBeforeRestore, heard.Count);
        PublishEvidence(evidence, evidenceId + "-after", battle, scene.Render(battle, 320, 224));
    }

    [Fact]
    public void Render_the_named_native_effect_checkpoint_at_its_current_boundary()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_COMPONENT_SNAPSHOT");
        Assert.SkipUnless(path is not null, "A concrete standalone native effect checkpoint is required.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!))!;
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        Assert.Equal(320 * 224 * 4, scene.Render(battle, 320, 224).Length);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
    }

    private static bool HasActionDescriptor(BattleSession battle)
    {
        NativeBattleDescriptorState descriptors = battle.NativeGraphics!.Descriptors;
        return descriptors.Queue.Any(slot => descriptors.Packets[slot].Command == 104);
    }

    private static void AssertNativeFileIoHostPump(BattleSnapshot entry)
    {
        //A controlled pending IO row isolates the outer scheduler from effect/model requests; the live battle stays unchanged.
        NativeBattleFileIoState pending = NativeBattleFileIoState.FromColdProcess().Queue482610(40960, 0x508470, null);
        BattleSession probe = BattleSession.Restore(entry with { NativeFileIo = pending, NativeEffectIoResult = -1 });
        long callback = probe.NativeGraphics!.Callback;
        probe.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Equal(callback + 1, probe.NativeGraphics.Callback);
        Assert.Equal((short)1, probe.NativeFileIo!.Rows[0].Countdown);
        Assert.Equal(-1, probe.NativeEffectIoResult);
        string mid = JsonSerializer.Serialize(probe.Capture());
        probe.Tick();
        Assert.Equal(mid, JsonSerializer.Serialize(probe.Capture()));
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(mid)!);
        for (int step = 0; step < 2; step++)
        {
            probe.AdvanceNativeGraphicsPresentation(Callback);
            clone.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(probe.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(step == 0 ? -1 : 40960, probe.NativeEffectIoResult);
        }
        Assert.Equal(callback + 3, probe.NativeGraphics.Callback);
        Assert.Equal((short)0, probe.NativeFileIo.ActiveCount);
        Assert.Equal((short)-1, probe.NativeFileIo.Rows[0].Countdown);
        NativeBattleGraphicsState graphics = entry.NativeGraphics!;
        NativeBattleUiServices services = graphics.UiServices!;
        BattleSession paused = BattleSession.Restore(entry with { NativeFileIo = pending, NativeEffectIoResult = -1, NativeGraphics = graphics with { UiServices = services with { Flags942 = (ushort)(services.Flags942 | 2), PauseDE9 = true } } });
        paused.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Equal(callback, paused.NativeGraphics!.Callback);
        Assert.Equal((short)2, paused.NativeFileIo!.Rows[0].Countdown);
        Assert.Equal(-1, paused.NativeEffectIoResult);
        BattleSession old = BattleSession.Restore(entry with { NativeFileIo = null });
        Assert.Throws<NotSupportedException>(() => old.AdvanceNativeGraphicsPresentation(Callback));
        Assert.Null(old.NativeFileIo);
        Assert.Equal(entry.NativeGraphics!.Callback, old.NativeGraphics!.Callback);
    }

    private static int DifferentPixels(byte[] visible, byte[] plain)
    {
        int count = 0;
        for (int pixel = 0; pixel < visible.Length; pixel += 4)
        {
            if (!visible.AsSpan(pixel, 4).SequenceEqual(plain.AsSpan(pixel, 4)))
            {
                count++;
            }
        }
        return count;
    }

    private static void PublishEvidence(string? directory, string name, BattleSession battle, byte[] pixels)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + ".battle.json"), JsonSerializer.Serialize(battle.Capture()));
        PublishPixels(directory, name, pixels);
    }

    private static void PublishPixels(string? directory, string name, byte[] pixels)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(pixels[offset + 3], pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }
        }
        bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
    }
}
