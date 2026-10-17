using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect3PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Installed_Thunder_effect3_runs_original_casting_pixels_sound_and_one_contact_through_restore()
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed original Effect3 resources are required.");
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleActionDefinition effect3 = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.MagicId == 3);
        Assert.Equal("Thunder", effect3.Name);
        BattleNativeAction nativeEffect3 = effect3.NativeAction ?? throw new InvalidDataException("Installed Effect3 lacks its original kernel action.");
        Assert.Equal((byte)2, nativeEffect3.Command);
        Assert.Equal(checked((ushort)effect3.Id), nativeEffect3.ActionId);
        Assert.Equal((ushort)3, nativeEffect3.MagicId);
        Console.WriteLine($"Installed effect3: {effect3.Name}, action{nativeEffect3.ActionId}, command{nativeEffect3.Command}, MagicId{nativeEffect3.MagicId}.");
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        BattleDefinition definition = new()
        {
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            Combatants = actors,
            EncounterId = 94,
            StageId = formation.StageId,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([3]),
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
        NativeBattleStatusSnapshot status = party.NativeStatus ?? throw new InvalidDataException("Effect3 component requires its live party status words.");
        //494410 calls47E2D0 before publishing this contact: party B90/B18 copy directly to B92/B1C.
        //This explicitly supplies the formula producer replaced by the controlled packet below; enemy DAT247 masks do not apply to actor0.
        BattleCombatantSnapshot preparedParty = party with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        before = before with { Combatants = before.Combatants.Select(actor => actor.Id == party.Id ? preparedParty : actor).ToArray() };
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, int header) = packets.Prepare(new(0, nativeEffect3.Command, 11, -1, nativeEffect3.ActionId, nativeEffect3.MagicId, null));
        //This controlled presentation component substitutes an already-computed7HP damage contact.
        //The installed kernel supplies the pose sequence; no battle formula or formula RNG is replayed.
        (packets, _) = packets.AppendContact(new(new(0, nativeEffect3.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        battle = BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
        BattleScenePresentation scene = new(assets, definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EFFECT3_EVIDENCE");
        string evidenceId = "effect3-casting-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        PublishEvidence(evidence, evidenceId + "-before", battle.Capture(), scene.Render(battle, 320, 224));
        bool selectedCasting = false;
        bool selectedEffect3 = false;
        bool sawSprites = false;
        bool continuedClone = false;
        bool exportedFirstEffectFrame = false;
        int contactTransitions = 0;
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
                selectedEffect3 |= currentGraphics.OriginalEffect is { Binding.Entry: 0x700C10, Pools.Length: 2 };
                ushort previousHp = beforeStep.NativePartyPresentation!.Rows[0]!.HitPoints;
                ushort presentedHp = snapshot.NativePartyPresentation!.Rows[0]!.HitPoints;
                Assert.True(presentedHp is 100 or 93, $"Controlled Effect3 contact changed the presented HP to{presentedHp}.");
                if (previousHp != presentedHp)
                {
                    Assert.Equal((ushort)100, previousHp);
                    Assert.Equal((ushort)93, presentedHp);
                    contactTransitions++;
                }
                if (clone is not null)
                {
                    clone.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224));
                    continuedClone = true;
                    clone = null;
                }
                NativeBattlePrimitiveFrame frame = currentGraphics.PrimitiveFrame ?? throw new InvalidDataException("Effect3 presentation requires its actual executed native primitive frame.");
                NativeBattleEffect3Resources? resources = currentGraphics.OriginalEffect?.Binding.Effect3Resources;
                if (resources is null)
                {
                    continue;
                }
                HashSet<uint> spriteResources = resources.Sprites.Select(resource => resource.Address).ToHashSet();
                HashSet<int> spriteDraws = currentGraphics.ModuleDraws.Select((draw, index) => (draw, index)).Where(pair => pair.draw.Kind == NativeBattleModuleDrawKind.Sprite && pair.draw.Resource is { } resource && spriteResources.Contains(resource.Address)).Select(pair => pair.index).ToHashSet();
                HashSet<int> effectDraws = new(spriteDraws);
                effectDraws.UnionWith(currentGraphics.ModuleDraws.Select((draw, index) => (draw, index)).Where(pair => pair.draw.Kind == NativeBattleModuleDrawKind.Flash && pair.draw.NativePrimitiveCommand == 0x2A && pair.draw.NativeOrderingBucket == 8).Select(pair => pair.index));
                foreach ((NativeBattleModuleDraw draw, int index) in currentGraphics.ModuleDraws.Select((draw, index) => (draw, index)).Where(pair => pair.draw.NativePrimitiveCommand == 0x2A))
                {
                    int offset = Assert.IsType<int>(draw.NativeEffectArenaOffset);
                    //Original B7CFB0 is NULL; B7D3B0 is461340, so command2A draws in the second render pass.
                    NativeBattleLogicalResolvedCommand command = Assert.Single(frame.LastCommands.Where(command => command.Pass == 2 && command.Command.ModuleDraw == index));
                    Assert.Equal("20DFAB8", command.Command.Packet.Bank);
                    Assert.Equal(offset, command.Command.Packet.ByteOffset);
                    Assert.Equal(5, battle.NativeEffectMemory!.Arena!.Read(offset + 3, 1)[0]);
                    Assert.Equal(new byte[] { 128, 128, 128, 0x2A }, battle.NativeEffectMemory.Arena.Read(offset + 4, 4));
                    Assert.Equal(offset + 24, currentGraphics.OriginalEffect!.DrawArenaOffset);
                }
                sawSprites |= spriteDraws.Count != 0;
                if (!frame.LastCommands.Any(command => command.Command.ModuleDraw is int index && effectDraws.Contains(index)))
                {
                    continue;
                }
                byte[] visible = scene.Render(battle, 320, 224).ToArray();
                if (!exportedFirstEffectFrame)
                {
                    PublishEvidence(evidence, evidenceId + "-first-effect-packets", snapshot, visible);
                    exportedFirstEffectFrame = true;
                }
                NativeBattlePrimitiveFrame withoutEffect = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.ModuleDraw is not int index || !effectDraws.Contains(index)).ToImmutableArray() };
                BattleSession plain = BattleSession.Restore(snapshot with { NativeGraphics = currentGraphics with { PrimitiveFrame = withoutEffect } });
                byte[] background = scene.Render(plain, 320, 224);
                int changedPixels = DifferentRgbPixels(visible, background);
                bool hasSpritePackets = frame.LastCommands.Any(command => command.Command.ModuleDraw is int index && spriteDraws.Contains(index));
                if (changedPixels == 0 || !hasSpritePackets)
                {
                    continue;
                }
                if (changedPixels > peakChangedPixels)
                {
                    peakChangedPixels = changedPixels;
                    peak = snapshot;
                    peakPixels = visible;
                }
                //700DE0 publishes counter5 immediately before its following callback's inline506690 contact.
                bool beforeContact = currentGraphics.OriginalEffect is { Pools.Length: 2 } currentEffect && currentEffect.Pools[1].Slots.Any(task => task.Callback == 0x700DE0 && task.ReadWord(12) == 5);
                if (!continuedClone && clone is null && beforeContact)
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
        Assert.True(selectedEffect3, "The installed Thunder700C10/700C20 controller never executed.");
        Assert.True(sawSprites, "Original700DE0 emitted none of its authenticated sprite resources.");
        if (peakChangedPixels == 0)
        {
            PublishEvidence(evidence, evidenceId + "-final-no-visible-effect", battle.Capture(), scene.Render(battle, 320, 224));
        }
        Assert.True(peakChangedPixels > 0, "Original Thunder sprite packets changed no RGB pixels.");
        Assert.True(continuedClone, "No visible Effect3 snapshot passed an independent next-callback continuation.");
        Assert.Equal(1, contactTransitions);
        Assert.Equal((ushort)93, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
        Assert.False(HasActionDescriptor(battle), "Original Effect3104 did not complete within the diagnostic callback bound.");
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal(before.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(before.Random, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
        NativeBattleModuleSound sound = Assert.Single(heard, sound => sound.Sound == 300006);
        Assert.Equal(0x101u, sound.Flags);
        Assert.NotNull(sound.Anchor);
        Assert.NotNull(sound.Pan);
        Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == 300006);
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
