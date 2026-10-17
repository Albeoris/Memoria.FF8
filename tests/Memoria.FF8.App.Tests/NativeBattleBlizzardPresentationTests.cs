using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBlizzardPresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Installed_Blizzard144_runs_original_geometry_casting_sound_contact_and_standalone_restore()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original Blizzard resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, item => item.NativeAction is { Command: 2, MagicId: 144 });
        Assert.Equal("Blizzard", action.Name);
        Assert.Equal(4, action.Id);
        BattleNativeAction native = action.NativeAction!;
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
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
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
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
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([144]),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_BLIZZARD_EVIDENCE");
        string id = "blizzard144-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleSnapshot entry = battle.Capture();
        Publish(evidence, id + "-before-entry", entry, null);
        battle = string.IsNullOrWhiteSpace(evidence) ? BattleSession.Restore(entry) : BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(evidence, id + "-before-entry.battle.json")))!);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot before = battle.Capture();
        BattleCombatantSnapshot party = before.Combatants.Single(actor => actor.Id == "party");
        NativeBattleStatusSnapshot status = party.NativeStatus!;
        before = before with { Combatants = before.Combatants.Select(actor => actor.Id == party.Id ? actor with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } } : actor).ToArray() };
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, int header) = packets.Prepare(new(0, native.Command, 11, -1, native.ActionId, native.MagicId, null));
        // An explicit post-formula7HP presentation contact; no formula or previous gameplay is replayed.
        (packets, _) = packets.AppendContact(new(new(0, native.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        battle = BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
        BattleScenePresentation scene = new(assets, definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        Publish(evidence, id + "-before", battle.Capture(), scene.Render(battle, 320, 224));
        bool casting = false;
        bool controller = false;
        bool mesh = false;
        bool composite = false;
        bool cloned = false;
        int contacts = 0;
        int cameraImpulses = 0;
        int peakRgb = 0;
        BattleSnapshot? peak = null;
        byte[]? peakPixels = null;
        BattleSession? independent = null;
        for (int callback = 0; callback < 1024 && HasDescriptor(battle); callback++)
        {
            BattleSnapshot prior = battle.Capture();
            try
            {
                battle.AdvanceNativeGraphicsPresentation(Callback);
                audio.Update(battle);
                audio.Update(battle);
                BattleSnapshot current = battle.Capture();
                NativeBattleGraphicsState currentGraphics = current.NativeGraphics!;
                NativeBattleCameraState camera = current.NativeCamera!;
                Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), camera.ViewTranslationScratch);
                if (camera.CurrentPose is not null)
                {
                    NativeBattleActionMatrix publishedView = camera.Projection!.View!;
                    NativeBattleActionMatrix baseView = camera.PublishProjection(0).Projection!.View!;
                    int impulse = unchecked(publishedView.Y - baseView.Y);
                    Assert.True(impulse is 0 or 32);
                    Assert.Equal(baseView.X, publishedView.X);
                    Assert.Equal(baseView.Z, publishedView.Z);
                    if (impulse != 0)
                    {
                        Assert.Equal((ushort)144, currentGraphics.OriginalEffect!.Binding.Effect);
                        cameraImpulses++;
                    }
                }
                casting |= currentGraphics.ActionGraphics?.Callback == 0x50A9A0;
                controller |= currentGraphics.OriginalEffect is { Binding: { Entry: 0x61ABA0, BlizzardResources: not null } };
                ushort hp = current.NativePartyPresentation!.Rows[0]!.HitPoints;
                Assert.True(hp is 100 or 93);
                if (hp != prior.NativePartyPresentation!.Rows[0]!.HitPoints)
                {
                    Assert.Equal((ushort)93, hp);
                    contacts++;
                }
                if (independent is not null)
                {
                    independent.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(independent, 320, 224).ToArray());
                    cloned = true;
                    independent = null;
                }
                NativeBattlePrimitiveFrame frame = currentGraphics.PrimitiveFrame!;
                if (currentGraphics.OriginalEffect is not { Binding: { Effect: 144, BlizzardResources: not null } } || !frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    continue;
                }
                mesh |= frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode is 0x24 or 0x26);
                foreach (NativeBattleLogicalResolvedCommand executed in frame.LastCommands.Where(command => command.Command.Packet.Bank == "20DFAB8" && command.Command.WordOffset == 2 && command.Command.Opcode == 0x26))
                {
                    NativeBattleEffectArena arena = current.NativeEffectMemory!.Arena!;
                    int offset = executed.Command.Packet.ByteOffset;
                    Assert.Equal((byte)9, arena.Read(offset + 3, 1)[0]);
                    Assert.Equal(0xE2080210u, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 4, 4)));
                    Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 36, 4)));
                    composite = true;
                }
                byte[] visible = scene.Render(battle, 320, 224).ToArray();
                NativeBattlePrimitiveFrame without = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                BattleSession plain = BattleSession.Restore(current with { NativeGraphics = currentGraphics with { PrimitiveFrame = without } });
                int rgb = DifferentRgb(visible, scene.Render(plain, 320, 224));
                if (rgb > peakRgb)
                {
                    peakRgb = rgb;
                    peak = current;
                    peakPixels = visible;
                }
                if (rgb > 0 && !cloned && independent is null)
                {
                    Publish(evidence, id + "-mid", current, visible);
                    BattleSnapshot saved = string.IsNullOrWhiteSpace(evidence) ? JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(current))! : JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(evidence, id + "-mid.battle.json")))!;
                    independent = BattleSession.Restore(saved);
                    Assert.Equal(visible, new BattleScenePresentation(assets, definition, true).Render(independent, 320, 224).ToArray());
                }
            }
            catch (Exception exception)
            {
                Publish(evidence, id + "-before-unsupported", prior, null);
                PublishJson(evidence, id + "-unsupported", new { prior.Frame, prior.NativeGraphics!.Callback, Error = exception.ToString() });
                throw;
            }
        }
        Assert.True(casting);
        Assert.True(controller);
        Assert.True(mesh, "No original Blizzard FT3 mesh packets were executed.");
        Assert.True(composite, "No original Blizzard E2/FT3/E2 composite packet was executed.");
        Assert.True(peakRgb > 0);
        Assert.True(cloned);
        Assert.Equal(1, contacts);
        Assert.Equal(1, cameraImpulses);
        Assert.False(HasDescriptor(battle));
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal(before.Random, battle.Capture().Random);
        Assert.Equal(before.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        NativeBattleModuleSound sound = Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == 300012).NativeModuleSound!;
        Assert.Equal(0x100u, sound.Flags);
        Assert.Single(heard, item => item.Sound == 300012);
        audio.Update(BattleSession.Restore(battle.Capture()));
        Assert.Single(heard, item => item.Sound == 300012);
        Publish(evidence, id + "-peak", peak!, peakPixels);
        Publish(evidence, id + "-after", battle.Capture(), scene.Render(battle, 320, 224));
        PublishJson(evidence, id + "-details", new { Effect = 144, PeakRgbPixels = peakRgb, PeakFrame = peak!.Frame, PeakCallback = peak.NativeGraphics!.Callback, FinishedFrame = battle.Frame, FinishedCallback = battle.NativeGraphics.Callback, ContactCount = contacts, OriginalSound = sound.Sound, CompositePacketObserved = composite, CameraImpulses = cameraImpulses });
        Console.WriteLine($"Blizzard144: peak{peakRgb}RGB at{peak.Frame}/{peak.NativeGraphics.Callback}, finished{battle.Frame}/{battle.NativeGraphics.Callback}, contact{contacts}, original sound{sound.Sound}, independent standalone continuation passed.");
    }

    [Fact]
    public void Resume_the_named_Blizzard_checkpoint_for_one_native_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_BLIZZARD_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(source), "A concrete standalone Blizzard current-cast or after-event checkpoint is required.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        Assert.Equal((ushort)144, snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        first.AdvanceNativeGraphicsPresentation(Callback);
        second.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Equal(snapshot.NativeGraphics.Callback + 1, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
        Console.WriteLine($"Blizzard checkpoint saved{snapshot.Frame}/{snapshot.NativeGraphics.Callback}; actual nextcallback{first.NativeGraphics.Callback}, independentwholeJSON/copiedpixels agree.");
    }

    private static bool HasDescriptor(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);

    private static int DifferentRgb(byte[] left, byte[] right)
    {
        int count = 0;
        for (int offset = 0; offset < left.Length; offset += 4)
        {
            if (left[offset] != right[offset] || left[offset + 1] != right[offset + 1] || left[offset + 2] != right[offset + 2])
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

    private static void Publish(string? directory, string name, BattleSnapshot snapshot, byte[]? pixels)
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
