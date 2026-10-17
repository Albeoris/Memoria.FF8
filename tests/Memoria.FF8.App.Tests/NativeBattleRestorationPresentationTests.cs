using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRestorationPresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Theory]
    [InlineData((ushort)24, "Esuna", 27, (byte)2)]
    [InlineData((ushort)27, "Full-life", 25, (byte)2)]
    [InlineData((ushort)67, "Remedy", 16, (byte)4)]
    public void Installed_restoration_cast_completes_original_nodes_pixels_sound_and_one_status_contact_through_restore(ushort effect, string name, int actionId, byte command)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original restoration resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Id == actionId && action.NativeAction is { } native && native.Command == command && native.MagicId == effect);
        Assert.Equal(name, action.Name);
        Assert.Equal(actionId, action.Id);
        BattleSession component = PrepareComponent(game, assets, action, effect);
        RunCurrentCast(component.Capture(), assets, effect, command == 2);
    }

    [Fact]
    public void Installed_unnamed_effect57_completes_its_controlled_original_descriptor_without_claiming_a_kernel_action()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original effect57 resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition shell = Assert.Single(assets.ActionDefinitions, action => action.NativeAction is { Command: 2, MagicId: 24 });
        //No original kernel reference for57 is known. Only the post-formula presentation header selects57;
        //the original Esuna command shell supplies its existing sequence/actor/contact layout, without running a formula.
        BattleSession component = PrepareComponent(game, assets, shell, 57, descriptorEffect: 57);
        RunCurrentCast(component.Capture(), assets, 57, true);
    }

    [Fact]
    public void Named_standalone_restoration_current_cast_resumes_without_formula_or_travel_replay()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_RESTORATION_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set MEMORIA_FF8_NATIVE_RESTORATION_SNAPSHOT to a concrete controlled restoration cast.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!))!;
        NativeBattleEffectBinding binding = source.NativeGraphics?.OriginalEffect?.Binding ?? Assert.Single(source.Definition.NativeEffectCatalog, binding => binding.RestorationResources is not null);
        Assert.True(binding.Effect is 24 or 27 or 57 or 67);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        RunCurrentCast(source, assets, binding.Effect, false);
    }

    [Fact]
    public void Resume_the_named_restoration_checkpoint_for_one_native_callback()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_RESTORATION_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "A concrete standalone restoration checkpoint is required.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
        Assert.True(source.NativeGraphics!.OriginalEffect!.Binding.Effect is 24 or 27 or 57 or 67);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(first.Capture()));
        first.AdvanceNativeGraphicsPresentation(Callback);
        second.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Equal(source.NativeGraphics.Callback + 1, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(second, 320, 224).ToArray());
        Publish(Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_RESTORATION_EVIDENCE"), "restoration-next-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"), first.Capture(), pixels);
        Console.WriteLine($"Restoration checkpoint effect{source.NativeGraphics.OriginalEffect.Binding.Effect}, saved{source.Frame}/{source.NativeGraphics.Callback}; actual nextcallback{first.NativeGraphics.Callback}, wholeJSON/copiedpixels agree.");
    }

    internal static BattleSession PrepareComponent(GameAssets game, BattleAssets assets, BattleActionDefinition action, ushort effect, bool applyKernelStatuses = false, ushort? descriptorEffect = null)
    {
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        const int targetMaximumHp = 200;
        bool resurrection = effect is 5 or 27 or 35;
        bool healing = effect is 53 or 58 or 59;
        int targetHp = effect == 27 || healing ? 200 : 100;
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "caster", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "target", Name = "Zell", IsPlayer = true, ModelId = 1, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id, MaximumHp = targetMaximumHp, CurrentHp = targetHp, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        //This fresh component declares the same source selectors as the battle-music entry fixture.
        //52CE30 transfers the original selected bank; no old snapshot's missing music is reconstructed.
        NativeMusicProcessState sourceBuffers = new(1, null, null, null, 0, null);
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(game.GameDirectory).ReadBattleEntryResources();
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicEntry.Prepare52CE30(94, 0x80, sourceBuffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = 94,
            StageId = formation.StageId,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([effect]),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        string? entryEvidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_RESTORATION_EVIDENCE") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EFFECT_PRESENTATION_EVIDENCE") ?? (effect is 5 or 35 ? Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_LIFE_EVIDENCE") : null);
        string entryId = $"restoration{effect}-native-entry-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        BattleSnapshot entrySource = battle.Capture();
        Publish(entryEvidence, entryId + "-before", entrySource, null);
        battle = string.IsNullOrWhiteSpace(entryEvidence) ? BattleSession.Restore(entrySource) : BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(entryEvidence, entryId + "-before.battle.json")))!);
        Assert.Equal(JsonSerializer.Serialize(entrySource), JsonSerializer.Serialize(battle.Capture()));
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot before = battle.Capture();
        Publish(entryEvidence, entryId + "-after", before, new BattleScenePresentation(assets, definition, true).Render(battle, 320, 224));
        BattleCombatantSnapshot target = before.Combatants.Single(actor => actor.Id == "target");
        BattleNativeAction native = action.NativeAction!;
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Restoration component lacks its current target status words.");
        //494360 adds saved0300 below one-quarter HP. Half HP for Esuna and full HP for Full-life keep this controlled result clear.
        Assert.Equal((ushort)0, status.Saved);
        //Controlled post-formula boundary: B90 already holds the successful result. B92 and the HP cache retain the prior display.
        //Esuna's prior poison bit is2; revival's prior KO bit is1. This does not run or admit either formula.
        ushort priorSaved = applyKernelStatuses || healing ? status.Saved : effect is 24 or 57 or 67 ? (ushort)2 : (ushort)1;
        uint priorBattle = status.Battle;
        if (applyKernelStatuses)
        {
            Assert.True(native.Statuses0 != 0 || native.Statuses1 != 0);
            //Controlled successful post-formula status result; this fixture does not execute the formula or its resistance roll.
            status = status with { Saved = (ushort)(status.Saved | native.Statuses0), Battle = status.Battle | native.Statuses1 };
        }
        target = target with { NativeStatus = status with { Presentation = new(priorSaved, priorBattle) } };
        NativeBattlePartyPresentation party = before.NativePartyPresentation!;
        NativeBattlePartyPresentedVital targetRow = party.Rows[1]!;
        party = party with { Rows = party.Rows.SetItem(1, targetRow with { HitPoints = healing ? (ushort)50 : resurrection ? (ushort)0 : targetRow.HitPoints }) };
        before = before with { Combatants = before.Combatants.Select(actor => actor.Id == target.Id ? target : actor).ToArray(), NativePartyPresentation = party, NativePartyStatusMirrors = before.NativePartyStatusMirrors!.Observe(1, priorSaved, priorBattle) };
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        byte sequence = definition.NativeCommandSequences[native.Command];
        (packets, int header) = packets.Prepare(new(0, native.Command, sequence, -1, native.ActionId, descriptorEffect ?? native.MagicId, null));
        ushort amount = healing ? checked((ushort)(targetHp - 50)) : resurrection ? checked((ushort)targetHp) : (ushort)0;
        (packets, _) = packets.AppendContact(new(new(1, native.DamagePresentation, 0, resurrection || healing ? (byte)1 : (byte)0, status.Saved, amount, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        NativeBattleGraphicsModel targetModel = graphics.Models[1];
        uint priorFlags = definition.NativeGraphics!.Rules.Project(priorSaved, priorBattle);
        //Controlled prior display status is newly added to this healthy-entry model:509D30..47 resets exactly its added timer bytes.
        uint addedPriorFlags = priorFlags & ~(targetModel.Flags8 ?? throw new InvalidDataException("Controlled prior status requires its existing graphics status."));
        ImmutableArray<byte?> priorTimers = targetModel.StatusTimers;
        for (int bit = 0; bit < 32; bit++)
        {
            if ((addedPriorFlags & (1u << bit)) != 0)
            {
                priorTimers = priorTimers.SetItem(bit, 0);
            }
        }
        graphics = graphics with { Models = graphics.Models.SetItem(1, targetModel with { Flags8 = priorFlags, StatusTimers = priorTimers }) };
        if (effect == 24)
        {
            BattleSession idle = BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics });
            BattleScenePresentation idleScene = new(assets, definition, true);
            string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_RESTORATION_EVIDENCE");
            string id = "restoration-poison-idle-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
            Publish(evidence, id + "-before", idle.Capture(), idleScene.Render(idle, 320, 224));
            idle.AdvanceNativeGraphicsPresentation(Callback);
            before = idle.Capture();
            graphics = before.NativeGraphics!;
            byte[] withIcons = idleScene.Render(idle, 320, 224).ToArray();
            Publish(evidence, id + "-after", before, withIcons);
            Assert.Equal(0u, graphics.Flags & 0x410);
            NativeBattleModuleDraw icon = Assert.Single(graphics.ModuleDraws, draw => draw.Resource?.Address == 0xC7EA38);
            int iconIndex = graphics.ModuleDraws.IndexOf(icon);
            NativeBattlePrimitiveFrame iconFrame = graphics.PrimitiveFrame!;
            Assert.Contains(iconFrame.LastCommands, command => command.Command.ModuleDraw == iconIndex);
            NativeBattlePrimitiveFrame plainFrame = iconFrame with { LastCommands = iconFrame.LastCommands.Where(command => command.Command.ModuleDraw != iconIndex).ToImmutableArray() };
            BattleSession plain = BattleSession.Restore(before with { NativeGraphics = graphics with { PrimitiveFrame = plainFrame } });
            int iconRgb = DifferentRgb(withIcons, idleScene.Render(plain, 320, 224));
            PublishJson(evidence, id + "-details", new { before.Frame, graphics.Callback, PoisonIconRgbPixels = iconRgb });
            Assert.True(iconRgb > 0, "Original poison icon packets changed no RGB pixels in the ordinary callback before104.");
        }
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        return BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
    }

    private static void RunCurrentCast(BattleSnapshot initial, BattleAssets assets, ushort effect, bool requireCasting)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleRestorationResources resources = Assert.IsType<NativeBattleRestorationResources>(initial.Definition.NativeEffectCatalog[effect - 1].RestorationResources);
        BattleSession battle = BattleSession.Restore(initial);
        Assert.True(HasDescriptor(battle), "The standalone restoration source must contain its current pending104 cast.");
        BattleScenePresentation scene = new(assets, initial.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_RESTORATION_EVIDENCE");
        string id = $"restoration{effect}-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        Publish(evidence, id + "-before", initial, scene.Render(battle, 320, 224));
        bool selectedCasting = initial.NativeGraphics?.ActionGraphics?.Callback == 0x50A9A0;
        bool selectedEffect = initial.NativeGraphics?.OriginalEffect?.Binding.Entry == profile.Entry;
        bool cleared = initial.NativeGraphics?.OriginalEffect?.Restoration?.Geometry is not null;
        bool clonePassed = false;
        bool faded = false;
        int statusTransitions = 0;
        int hpTransitions = 0;
        int expectedStatusTransitions = initial.Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation.Saved == 0 ? 0 : 1;
        int expectedHpTransitions = effect == 27 && initial.NativePartyPresentation!.Rows[1]!.HitPoints == 0 ? 1 : 0;
        NativeBattleTextureCopy? expectedContactCopy = null;
        if (effect == 27 && expectedStatusTransitions != 0)
        {
            NativeBattleGraphicsModel targetModel = initial.NativeGraphics!.Models[1];
            NativeBattleGraphicsResource resource = initial.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == "target").Resource;
            Assert.Equal(2u, targetModel.Flags8!.Value & 2);
            expectedContactCopy = NativeBattleTextureMotion.Plan50C780(resource.TextureAnimations, targetModel.TextureAllocationMask, 0, 0);
            Assert.NotNull(expectedContactCopy);
        }
        int peakRgb = 0;
        BattleSnapshot? peak = null;
        byte[]? peakPixels = null;
        BattleSession? clone = null;
        for (int callback = 0; callback < 1024 && HasDescriptor(battle); callback++)
        {
            BattleSnapshot before = battle.Capture();
            try
            {
                battle.AdvanceNativeGraphicsPresentation(Callback);
                audio.Update(battle);
                audio.Update(battle);
                BattleSnapshot after = battle.Capture();
                NativeBattleGraphicsState graphics = after.NativeGraphics!;
                selectedCasting |= graphics.ActionGraphics?.Callback == 0x50A9A0;
                selectedEffect |= graphics.OriginalEffect is { Restoration: not null } current && current.Binding.Entry == profile.Entry;
                faded |= battle.NativeStage?.Groups.Any(group => group.ColorDepth > 0) == true;
                if (!cleared && graphics.OriginalEffect?.Restoration is { Emitters: int emitters, Geometry: int geometry })
                {
                    Assert.Equal(emitters + profile.EmitterBytes, geometry);
                    Assert.All(after.NativeEffectMemory!.Arena!.Read(emitters, profile.EmitterBytes + profile.GeometryBytes), value => Assert.Equal((byte)0, value));
                    Assert.Equal(before.NativeCrtRandom, after.NativeCrtRandom);
                    cleared = true;
                }
                ushort? priorStatus = before.Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation.Saved;
                ushort? currentStatus = after.Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation.Saved;
                if (priorStatus != currentStatus)
                {
                    Assert.Equal(initial.Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation.Saved, priorStatus);
                    Assert.Equal((ushort)0, currentStatus);
                    Assert.Equal(0u, graphics.Models[1].Flags8!.Value & initial.Definition.NativeGraphics!.Rules.Project(priorStatus!.Value, 0));
                    if (expectedContactCopy is { } copy)
                    {
                        NativeBattleEffectVramCopy expected = new(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
                        Assert.Contains(after.NativeEffectMemory!.Textures!.Slots, slot => slot?.Kind == 3 && slot.Copy == expected);
                    }
                    statusTransitions++;
                }
                ushort priorHp = before.NativePartyPresentation!.Rows[1]!.HitPoints;
                ushort currentHp = after.NativePartyPresentation!.Rows[1]!.HitPoints;
                if (priorHp != currentHp)
                {
                    Assert.Equal((ushort)0, priorHp);
                    Assert.Equal((ushort)200, currentHp);
                    hpTransitions++;
                }
                if (clone is not null)
                {
                    clone.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, initial.Definition, true).Render(clone, 320, 224));
                    clonePassed = true;
                    clone = null;
                }
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
                if ((graphics.Flags & 0x410) != 0)
                {
                    Assert.DoesNotContain(graphics.ModuleDraws, draw => draw.Resource?.Address == 0xC7EA38);
                }
                if (!frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    continue;
                }
                byte[] visible = scene.Render(battle, 320, 224).ToArray();
                NativeBattlePrimitiveFrame plainFrame = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                BattleSession plain = BattleSession.Restore(after with { NativeGraphics = graphics with { PrimitiveFrame = plainFrame } });
                int rgb = DifferentRgb(visible, scene.Render(plain, 320, 224));
                if (rgb > peakRgb)
                {
                    peakRgb = rgb;
                    peak = after;
                    peakPixels = visible;
                }
                if (rgb > 0 && !clonePassed && clone is null)
                {
                    Publish(evidence, id + "-mid", after, visible);
                    string json = string.IsNullOrWhiteSpace(evidence) ? JsonSerializer.Serialize(after) : File.ReadAllText(Path.Combine(evidence, id + "-mid.battle.json"));
                    clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
                    Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(visible, new BattleScenePresentation(assets, initial.Definition, true).Render(clone, 320, 224));
                }
            }
            catch (Exception error)
            {
                Publish(evidence, id + "-before-unsupported-callback" + before.NativeGraphics!.Callback, before, null);
                PublishJson(evidence, id + "-unsupported-details", new { Effect = effect, before.Frame, Callback = before.NativeGraphics!.Callback, Error = error.ToString() });
                throw;
            }
        }
        if (peakRgb == 0)
        {
            Publish(evidence, id + "-no-visible-effect", battle.Capture(), scene.Render(battle, 320, 224));
        }
        Assert.True(selectedCasting || !requireCasting);
        Assert.True(selectedEffect);
        Assert.True(cleared);
        Assert.True(faded || !requireCasting);
        Assert.True(peakRgb > 0, "Original restoration effect-bank packets changed no RGB pixels.");
        Assert.True(clonePassed);
        Assert.Equal(expectedStatusTransitions, statusTransitions);
        Assert.Equal(expectedHpTransitions, hpTransitions);
        Assert.Equal((ushort)0, battle.Capture().Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation.Saved);
        Assert.Equal(effect == 27 ? (ushort)200 : (ushort)100, battle.Capture().NativePartyPresentation!.Rows[1]!.HitPoints);
        Assert.False(HasDescriptor(battle));
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal(0, battle.NativeGraphics.Models[1].Flags0 & 4);
        Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
        Assert.Equal(initial.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(initial.Random, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
        NativeBattleModuleSound sound = Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == resources.Sound).NativeModuleSound!;
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Equal(initial.Events.Count(entry => entry.NativeModuleSound?.Sound == resources.Sound) == 0 ? 1 : 0, heard.Count(sound => sound.Sound == resources.Sound));
        int heardBeforeRestore = heard.Count;
        audio.Update(BattleSession.Restore(battle.Capture()));
        Assert.Equal(heardBeforeRestore, heard.Count);
        Publish(evidence, id + "-peak", peak!, peakPixels);
        Publish(evidence, id + "-after", battle.Capture(), scene.Render(battle, 320, 224));
        PublishJson(evidence, id + "-details", new { Effect = effect, PeakRgbPixels = peakRgb, PeakFrame = peak!.Frame, PeakCallback = peak.NativeGraphics!.Callback, FinishedFrame = battle.Frame, FinishedCallback = battle.NativeGraphics.Callback, StatusTransitions = statusTransitions, HpTransitions = hpTransitions, OriginalSound = resources.Sound });
        Console.WriteLine($"Restoration{effect}: RGBpeak{peakRgb}, frame{peak.Frame}/callback{peak.NativeGraphics!.Callback}, finished{battle.Frame}/{battle.NativeGraphics.Callback}, status contacts{statusTransitions}, HP contacts{hpTransitions}, evidence{evidence ?? "disabled"}.");
    }

    private static bool HasDescriptor(BattleSession battle)
    {
        NativeBattleDescriptorState descriptors = battle.NativeGraphics!.Descriptors;
        return descriptors.Queue.Any(slot => descriptors.Packets[slot].Command == 104);
    }

    private static int DifferentRgb(byte[] first, byte[] second)
    {
        int changed = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        return changed;
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

    internal static void Publish(string? directory, string name, BattleSnapshot snapshot, byte[]? pixels)
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
