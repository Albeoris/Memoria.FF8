using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCuragaPresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Standalone_Curaga_snapshot_keeps_native_pool_clear_without_random_draws_and_completes_current_cast()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_CURAGA_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set MEMORIA_FF8_NATIVE_CURAGA_SNAPSHOT to an intact standalone Curaga save before87DE10.");
        BattleSnapshot initial = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!))!;
        BattleSession battle = BattleSession.Restore(initial);
        Assert.True(HasActionDescriptor(battle));
        Assert.Null(initial.NativeGraphics!.OriginalEffect?.Curaga?.Auxiliary16F80);
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using BattleAssets assets = new(gameDirectory);
        BattleScenePresentation scene = new(assets, initial.Definition, true);
        bool cleared = false;
        bool continuedClone = false;
        int peakChangedPixels = 0;
        int nonFlashPeakChangedPixels = 0;
        BattleSnapshot? nonFlashPeak = null;
        byte[]? nonFlashPixels = null;
        BattleSession? clone = null;
        for (int callback = 0; callback < 1024 && HasActionDescriptor(battle); callback++)
        {
            BattleSnapshot before = battle.Capture();
            battle.AdvanceNativeGraphicsPresentation(Callback);
            BattleSnapshot after = battle.Capture();
            NativeBattleCuragaState? state = after.NativeGraphics!.OriginalEffect?.Curaga;
            if (!cleared && state?.Auxiliary16F80 is int geometry)
            {
                int emitter = state.Auxiliary25F8!.Value;
                Assert.Equal(emitter + 0x25F8, geometry);
                Assert.All(after.NativeEffectMemory!.Arena!.Read(emitter, 0x25F8 + 0x16F80), value => Assert.Equal((byte)0, value));
                Assert.Equal(before.NativeCrtRandom, after.NativeCrtRandom);
                cleared = true;
                clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(after))!);
                continue;
            }
            if (clone is not null)
            {
                clone.AdvanceNativeGraphicsPresentation(Callback);
                Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(clone.Capture()));
                Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, initial.Definition, true).Render(clone, 320, 224));
                continuedClone = true;
                clone = null;
            }
            NativeBattleGraphicsState graphics = after.NativeGraphics!;
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
            if (!frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
            {
                continue;
            }
            byte[] visible = scene.Render(battle, 320, 224).ToArray();
            NativeBattlePrimitiveFrame withoutEffect = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
            BattleSession plain = BattleSession.Restore(after with { NativeGraphics = graphics with { PrimitiveFrame = withoutEffect } });
            int changedPixels = DifferentRgbPixels(visible, scene.Render(plain, 320, 224));
            peakChangedPixels = Math.Max(peakChangedPixels, changedPixels);
            if (changedPixels > nonFlashPeakChangedPixels && !frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0x2A))
            {
                nonFlashPeakChangedPixels = changedPixels;
                nonFlashPeak = after;
                nonFlashPixels = visible;
            }
        }
        Assert.True(cleared);
        Assert.True(continuedClone);
        Assert.True(peakChangedPixels > 0);
        Assert.True(nonFlashPeakChangedPixels > 0);
        Assert.False(HasActionDescriptor(battle));
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal((ushort)200, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
        Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == 0x4946F);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_CURAGA_EVIDENCE");
        string evidenceId = "curaga-resumed-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        BattleSnapshot finished = battle.Capture();
        PublishEvidence(evidence, evidenceId + "-non-flash-peak", nonFlashPeak!, nonFlashPixels);
        PublishEvidence(evidence, evidenceId + "-after", finished, scene.Render(battle, 320, 224));
        string afterJson = string.IsNullOrWhiteSpace(evidence) ? JsonSerializer.Serialize(finished) : File.ReadAllText(Path.Combine(evidence, evidenceId + "-after.battle.json"));
        BattleSession settledClone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(afterJson)!);
        int soundsBefore = battle.Events.Count(entry => entry.NativeModuleSound?.Sound == 0x4946F);
        battle.AdvanceNativeGraphicsPresentation(Callback);
        settledClone.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(settledClone.Capture()));
        Assert.Equal(soundsBefore, battle.Events.Count(entry => entry.NativeModuleSound?.Sound == 0x4946F));
        Assert.Equal((ushort)200, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
        Assert.False(HasActionDescriptor(battle));
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        PublishEvidence(evidence, evidenceId + "-after-validation", battle.Capture(), null);
        PublishJson(evidence, evidenceId + "-details", new { Source = Path.GetFullPath(path!), PeakChangedPixelsIncludingFlash = peakChangedPixels, NonFlashPeakChangedPixels = nonFlashPeakChangedPixels, NonFlashPeakFrame = nonFlashPeak!.Frame, NonFlashPeakCallback = nonFlashPeak.NativeGraphics!.Callback, FinishedFrame = finished.Frame, FinishedCallback = finished.NativeGraphics!.Callback, ValidationFrame = battle.Frame, ValidationCallback = battle.NativeGraphics.Callback });
    }

    [Fact]
    public void Installed_Curaga_casting_runs_original_callbacks_pixels_sound_and_one_healing_contact_through_restore()
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed original Curaga resources are required.");
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleActionDefinition curaga = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.MagicId == 28);
        BattleNativeAction nativeCuraga = curaga.NativeAction ?? throw new InvalidDataException("Installed Curaga lacks its original kernel action.");
        Assert.Equal((byte)2, nativeCuraga.Command);
        Assert.Equal(checked((ushort)curaga.Id), nativeCuraga.ActionId);
        Assert.Equal((ushort)28, nativeCuraga.MagicId);
        Console.WriteLine($"Installed effect28: {curaga.Name}, action{nativeCuraga.ActionId}, command{nativeCuraga.Command}, MagicId{nativeCuraga.MagicId}.");
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = 94,
            StageId = formation.StageId,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([28]),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot before = battle.Capture();
        BattleCombatantSnapshot party = before.Combatants.Single(actor => actor.Id == "party");
        Assert.Empty(party.StatusEffects);
        NativeBattleStatusSnapshot status = party.NativeStatus ?? throw new InvalidDataException("Curaga component requires its live party status words.");
        //494410 calls47E2D0 before publishing this contact: party B90/B18 copy directly to B92/B1C.
        //This explicitly supplies the formula producer replaced by the controlled packet below; enemy DAT247 masks do not apply to actor0.
        BattleCombatantSnapshot preparedParty = party with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        before = before with { Combatants = before.Combatants.Select(actor => actor.Id == party.Id ? preparedParty : actor).ToArray() };
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, int header) = packets.Prepare(new(0, nativeCuraga.Command, 11, -1, nativeCuraga.ActionId, nativeCuraga.MagicId, null));
        //This is a controlled presentation component, after the battle formula has already produced its healing contact.
        //The installed kernel supplies the pose sequence;100 HP isolates506690/493D80 timing and duplicate application.
        (packets, _) = packets.AppendContact(new(new(0, nativeCuraga.DamagePresentation, 0, 1, status.Saved, 100, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        battle = BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
        BattleScenePresentation scene = new(assets, definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_CURAGA_EVIDENCE");
        string evidenceId = "curaga-casting-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        PublishEvidence(evidence, evidenceId + "-before", battle.Capture(), scene.Render(battle, 320, 224));
        bool selectedCasting = false;
        bool selectedCuraga = false;
        bool stageFaded = false;
        bool continuedClone = false;
        bool exportedFirstEffectFrame = false;
        bool observedPoolClear = false;
        int healingTransitions = 0;
        int peakChangedPixels = 0;
        BattleSnapshot? peak = null;
        byte[]? peakPixels = null;
        BattleSession? clone = null;
        for (int callback = 0; callback < 1024 && HasActionDescriptor(battle); callback++)
        {
            BattleSnapshot beforeStep = battle.Capture();
            try
            {
                battle.AdvanceNativeGraphicsPresentation(Callback);
                audio.Update(battle);
                audio.Update(battle);
                BattleSnapshot snapshot = battle.Capture();
                NativeBattleGraphicsState currentGraphics = snapshot.NativeGraphics!;
                if (!observedPoolClear && currentGraphics.OriginalEffect?.Curaga is { Auxiliary25F8: int emitters, Auxiliary16F80: int geometry })
                {
                    Assert.Equal(emitters + 0x25F8, geometry);
                    Assert.All(snapshot.NativeEffectMemory!.Arena!.Read(emitters, 0x25F8 + 0x16F80), value => Assert.Equal((byte)0, value));
                    Assert.Equal(beforeStep.NativeCrtRandom, snapshot.NativeCrtRandom);
                    observedPoolClear = true;
                }
                selectedCasting |= currentGraphics.ActionGraphics?.Callback == 0x50A9A0;
                selectedCuraga |= currentGraphics.OriginalEffect is { Binding.Entry: 0x87DB10, Curaga: not null };
                stageFaded |= battle.NativeStage?.Groups.Any(group => group.ColorDepth > 0) == true;
                ushort previousHp = beforeStep.NativePartyPresentation!.Rows[0]!.HitPoints;
                ushort presentedHp = snapshot.NativePartyPresentation!.Rows[0]!.HitPoints;
                Assert.True(presentedHp is 100 or 200, $"Controlled Curaga contact changed the presented HP to{presentedHp}.");
                if (previousHp != presentedHp)
                {
                    Assert.Equal((ushort)100, previousHp);
                    Assert.Equal((ushort)200, presentedHp);
                    healingTransitions++;
                }
                if (clone is not null)
                {
                    clone.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224));
                    continuedClone = true;
                    clone = null;
                }
                NativeBattlePrimitiveFrame frame = currentGraphics.PrimitiveFrame ?? throw new InvalidDataException("Curaga presentation requires its actual executed native primitive frame.");
                if (!frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    continue;
                }
                byte[] visible = scene.Render(battle, 320, 224).ToArray();
                if (!exportedFirstEffectFrame)
                {
                    PublishEvidence(evidence, evidenceId + "-first-effect-packets", snapshot, visible);
                    exportedFirstEffectFrame = true;
                }
                NativeBattlePrimitiveFrame withoutEffect = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                BattleSession plain = BattleSession.Restore(snapshot with { NativeGraphics = currentGraphics with { PrimitiveFrame = withoutEffect } });
                byte[] background = scene.Render(plain, 320, 224);
                int changedPixels = DifferentRgbPixels(visible, background);
                if (changedPixels <= peakChangedPixels)
                {
                    continue;
                }
                peakChangedPixels = changedPixels;
                peak = snapshot;
                peakPixels = visible;
                if (!continuedClone && clone is null)
                {
                    string json = JsonSerializer.Serialize(snapshot);
                    clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
                    Assert.Equal(json, JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(visible, new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224));
                    PublishEvidence(evidence, evidenceId + "-mid", snapshot, visible);
                }
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
            {
                NativeBattleGraphicsState priorGraphics = beforeStep.NativeGraphics!;
                PublishEvidence(evidence, evidenceId + "-before-unsupported-callback" + priorGraphics.Callback, beforeStep, null);
                PublishJson(evidence, evidenceId + "-unsupported-details", new { exception.Message, beforeStep.Frame, priorGraphics.Callback, Root = priorGraphics.OriginalEffect?.ReturnedDescriptor });
                throw;
            }
        }
        Assert.True(selectedCasting, "Native104 did not select original50A9A0 casting.");
        Assert.True(selectedCuraga, "The installed Curaga87DB10 initializer/controller never executed.");
        Assert.True(observedPoolClear, "Original87DE10 did not preserve both cleared auxiliary pools through its first child callback.");
        Assert.True(stageFaded, "Original Curaga never executed its four-group stage fade.");
        if (peakChangedPixels == 0)
        {
            PublishEvidence(evidence, evidenceId + "-final-no-visible-effect", battle.Capture(), scene.Render(battle, 320, 224));
        }
        Assert.True(peakChangedPixels > 0, "Original Curaga effect-bank packets changed no RGB pixels.");
        Assert.True(continuedClone, "No visible Curaga snapshot passed an independent next-callback continuation.");
        Assert.Equal(1, healingTransitions);
        Assert.Equal((ushort)200, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
        Assert.False(HasActionDescriptor(battle), "Original Curaga104 did not complete within the diagnostic callback bound.");
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal(0, battle.NativeGraphics.Models[0].Flags0 & 4);
        Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
        Assert.Equal(before.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(before.Random, battle.Capture().Random);
        Assert.NotEqual(before.NativeCrtRandom, battle.Capture().NativeCrtRandom);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
        NativeBattleModuleSound sound = Assert.Single(heard, sound => sound.Sound == 0x4946F);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == 0x4946F);
        int heardBeforeRestore = heard.Count;
        audio.Update(BattleSession.Restore(battle.Capture()));
        Assert.Equal(heardBeforeRestore, heard.Count);
        PublishEvidence(evidence, evidenceId + "-peak", peak!, peakPixels);
        PublishJson(evidence, evidenceId + "-peak-details", new { ChangedRgbPixels = peakChangedPixels, Callback = peak!.NativeGraphics!.Callback, peak.Frame });
        PublishEvidence(evidence, evidenceId + "-after", battle.Capture(), scene.Render(battle, 320, 224));
    }

    private static bool HasActionDescriptor(BattleSession battle)
    {
        NativeBattleDescriptorState descriptors = battle.NativeGraphics!.Descriptors;
        return descriptors.Queue.Any(slot => descriptors.Packets[slot].Command == 104);
    }

    private static int DifferentRgbPixels(byte[] first, byte[] second)
    {
        int count = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                count++;
            }
        }
        return count;
    }

    private static void PublishJson(string? directory, string name, object value)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        using FileStream stream = new(Path.Combine(directory, name + ".json"), FileMode.CreateNew);
        JsonSerializer.Serialize(stream, value, value.GetType());
    }

    private static void PublishEvidence(string? directory, string name, BattleSnapshot snapshot, byte[]? pixels)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        PublishJson(directory, name + ".battle", snapshot);
        if (pixels is null)
        {
            return;
        }
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(pixels[offset + 3], pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }
        }
        using FileStream stream = new(Path.Combine(directory, name + ".png"), FileMode.CreateNew);
        bitmap.Save(stream, ImageFormat.Png);
    }
}
