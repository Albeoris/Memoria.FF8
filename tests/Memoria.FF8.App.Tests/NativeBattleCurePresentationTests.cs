using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCurePresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Installed_Cure_casting_runs_original_callbacks_pixels_sound_and_one_healing_contact_through_restore()
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed original Cure resources are required.");
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleActionDefinition cure = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 21);
        BattleNativeAction nativeCure = cure.NativeAction ?? throw new InvalidDataException("Installed Cure lacks its original kernel action.");
        Assert.Equal((ushort)1, nativeCure.MagicId);
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
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([1]),
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
        NativeBattleStatusSnapshot status = party.NativeStatus ?? throw new InvalidDataException("Cure component requires its live party status words.");
        //494410 calls47E2D0 before publishing this contact: party B90/B18 copy directly to B92/B1C.
        //This explicitly supplies the formula producer replaced by the controlled packet below; enemy DAT247 masks do not apply to actor0.
        BattleCombatantSnapshot preparedParty = party with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        before = before with { Combatants = before.Combatants.Select(actor => actor.Id == party.Id ? preparedParty : actor).ToArray() };
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, int header) = packets.Prepare(new(0, 2, 11, -1, 21, 1, null));
        //This is a controlled presentation component, after the battle formula has already produced its healing contact.
        //The installed kernel supplies the pose sequence;100 HP isolates506690/493D80 timing and duplicate application.
        (packets, _) = packets.AppendContact(new(new(0, nativeCure.DamagePresentation, 0, 1, status.Saved, 100, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        battle = BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
        BattleScenePresentation scene = new(assets, definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(() => { }, _ => { }, _ => { }, playModule: heard.Add);
        audio.Update(battle);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_CURE_EVIDENCE");
        string evidenceId = "cure-casting-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        PublishEvidence(evidence, evidenceId + "-before", battle.Capture(), scene.Render(battle, 320, 224));
        bool selectedCasting = false;
        bool selectedCure = false;
        bool hiddenTarget = false;
        bool continuedClone = false;
        bool exportedFirstEffectFrame = false;
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
                selectedCasting |= currentGraphics.ActionGraphics?.Callback == 0x50A9A0;
                selectedCure |= currentGraphics.OriginalEffect is { Binding.Entry: 0x8D6A00, Cure: not null };
                hiddenTarget |= (currentGraphics.Models[0].Flags0 & 4) != 0;
                ushort previousHp = beforeStep.NativePartyPresentation!.Rows[0]!.HitPoints;
                ushort presentedHp = snapshot.NativePartyPresentation!.Rows[0]!.HitPoints;
                Assert.True(presentedHp is 100 or 200, $"Controlled Cure contact changed the presented HP to{presentedHp}.");
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
                NativeBattlePrimitiveFrame frame = currentGraphics.PrimitiveFrame ?? throw new InvalidDataException("Cure presentation requires its actual executed native primitive frame.");
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
        Assert.True(selectedCure, "The installed Cure8D6A00 initializer/controller never executed.");
        Assert.True(hiddenTarget, "Original Cure actor assembly never hid the ordinary target model.");
        if (peakChangedPixels == 0)
        {
            PublishEvidence(evidence, evidenceId + "-final-no-visible-effect", battle.Capture(), scene.Render(battle, 320, 224));
        }
        Assert.True(peakChangedPixels > 0, "Original Cure effect-bank packets changed no RGB pixels.");
        Assert.True(continuedClone, "No visible Cure snapshot passed an independent next-callback continuation.");
        Assert.Equal(1, healingTransitions);
        Assert.Equal((ushort)200, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
        Assert.False(HasActionDescriptor(battle), "Original Cure104 did not complete within the diagnostic callback bound.");
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal(0, battle.NativeGraphics.Models[0].Flags0 & 4);
        Assert.Equal(before.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(before.Random, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
        NativeBattleModuleSound sound = Assert.Single(heard, sound => sound.Sound == 0x493F2);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == 0x493F2);
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
