using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleQuake38PresentationTests
{
    [Fact]
    public void Original_Quake_completes_ground_split_ripples_particles_contact_sound_and_standalone_next()
    {
        string? manifest = Environment.GetEnvironmentVariable("MEMORIA_FF8_QUAKE_CASES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifest), "An external ordinary Magic case manifest is required.");
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_QUAKE_CASE") ?? throw new InvalidDataException("One external case identity is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_QUAKE_OUTPUT") ?? throw new InvalidDataException("A distinct external case output directory is required.");
        NativeBattleOrdinaryMagicCase item = (JsonSerializer.Deserialize<NativeBattleOrdinaryMagicCases>(File.ReadAllBytes(manifest!)) ?? throw new InvalidDataException("The case manifest is null.")).Cases.Single(item => item.Id == id);
        Assert.Equal((ushort)38, item.Effect);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.MagicId == item.Effect && (item.ActionId is null || action.ActionId == item.ActionId));
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([item.Effect])[item.Effect - 1];
        string? supplied = Environment.GetEnvironmentVariable("MEMORIA_FF8_QUAKE_SNAPSHOT");
        string sourcePath;
        if (string.IsNullOrWhiteSpace(supplied))
        {
            NativeEffectFixtureRequest request = new(item.Id, item.Effect, 2, action.ActionId, 3, item.EntryMaximumCallbacks, item.ProcessTimeoutSeconds) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = item.ProducerMaximumCallbacks };
            string fixture = Path.Combine(output, "fixture");
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixture);
            File.WriteAllText(Path.Combine(output, "fixture-result.json"), JsonSerializer.Serialize(exported, new JsonSerializerOptions { WriteIndented = true }));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            sourcePath = Path.Combine(fixture, "ordinary-produced-action104.battle.json");
        }
        else
        {
            sourcePath = supplied;
        }
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath);
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(Path.GetDirectoryName(sourcePath)!, "before-ordinary-input.battle.json"));
        NativeBattleGraphicsState sourceGraphics = source.NativeGraphics!;
        int descriptor = Assert.Single(sourceGraphics.Descriptors.Queue, index => sourceGraphics.Descriptors.Packets[index].Command == 104);
        uint relative = unchecked(sourceGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
        Assert.True(relative % 20 == 0 && relative / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(relative / 20))];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal(item.Effect, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[item.Effect - 1]));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.True(contact.Primary.Amount6 > 0);
        string target = sourceGraphics.ActorIds[3]!;
        string caster = sourceGraphics.ActorIds[0]!;
        Assert.Equal(2, Stock(beforeInput, caster, action.ActionId));
        Assert.Equal(1, Stock(source, caster, action.ActionId));
        int producedHp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
        Assert.True(beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp > producedHp);
        NativeBattleStageGroup originalGround = source.NativeStage!.Groups[1];
        Assert.NotEqual(0, originalGround.Flags & 1);
        Assert.Contains(source.Definition.NativeStageSource!.Geometry!.Value, mesh => mesh.StageGroup == 1 && mesh.Polygons.Length != 0);
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> pending = ["initialized", "ripple"];
        HashSet<short> groundStates = [];
        HashSet<uint> particles = [];
        int contacts = 0;
        int completions = 0;
        int peak = 0;
        int peakCracks = 0;
        bool cameraChanged = false;
        bool groundHidden = false;
        bool bounced = false;
        int ticks = 0;
        BattleSnapshot? prior = null;
        try
        {
            ColdNext("produced", source, false);
            for (; ticks < item.MaximumTicks; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                NativeBattleQuake38State? priorState = prior.NativeEffectMemory?.Quake38;
                NativeBattleModulePoolTask? emitter = Emitter(before);
                bool contactReady = emitter is not null && emitter.ReadByte(41) == 1 && emitter.ReadByte(40) == 0;
                string? boundary = before?.ReturnedDescriptor is > 0 && pending.Contains("initialized") ? "initialized" : priorState?.Word(0x279299E) == 5 && pending.Contains("ripple") ? "ripple" : null;
                if (boundary is not null)
                {
                    ColdNext(boundary, prior);
                    pending.Remove(boundary);
                }
                else
                {
                    battle.Tick();
                }
                BattleSnapshot current = battle.Capture();
                NativeBattleOriginalEffectState? after = Selected(current);
                NativeBattleQuake38State? state = current.NativeEffectMemory?.Quake38;
                Assert.Equal(producedHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(1, Stock(current, caster, action.ActionId));
                if (state is not null && after?.ReturnedDescriptor is > 0)
                {
                    groundStates.Add(state.Word(0x279299E));
                    foreach (NativeBattleModulePoolTask task in after.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0))
                    {
                        particles.Add(task.Callback);
                    }
                    groundHidden |= (current.NativeStage!.Groups[1].Flags & 2) == 0;
                    NativeBattleModulePoolTask? sequence = after.Pools.SelectMany(pool => pool.Slots).SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x8D8800);
                    if (sequence?.ReadByte(41) == 7 && state.SavedActors[3].Position28 is { } saved && current.NativeGraphics!.Models[3].Transform is { } transform)
                    {
                        bounced |= transform.Position28.Y != saved.Y;
                    }
                }
                cameraChanged |= JsonSerializer.Serialize(current.NativeCamera?.Projection) != JsonSerializer.Serialize(source.NativeCamera?.Projection);
                if (contactReady)
                {
                    Assert.True(Emitter(after) is null || Emitter(after)!.ReadByte(41) == 2);
                    contacts++;
                    File.WriteAllText(Path.Combine(output, "emitter-contact-receipt.json"), JsonSerializer.Serialize(new { BeforeCallback = prior.NativeGraphics!.Callback, AfterCallback = current.NativeGraphics!.Callback, Emitter = emitter, Contact = contact, TargetHp = producedHp, Observation = "Original emitter consumes its complete installed action target list after its linked sequence and camera tasks release; ordinary producer HP remains unchanged" }, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                NativeBattlePrimitiveFrame frame = current.NativeGraphics!.PrimitiveFrame!;
                if (frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    int changed = Isolate(current, visible, command => command.Command.Packet.Bank == "20DFAB8");
                    if (changed > peak)
                    {
                        peak = changed;
                        NativeBattleRestorationPresentationTests.Publish(output, $"visible-peak-{current.NativeGraphics.Callback}", current, visible);
                    }
                    bool Crack(NativeBattleLogicalResolvedCommand command) => command.Command.Packet.Bank == "20DFAB8" && (command.Command.Opcode & 0xFC) == 0x2C && command.Command.Words.Length >= 3 && (command.Command.Words[2] >> 16) == 0x3C54;
                    if (frame.LastCommands.Any(Crack))
                    {
                        int cracks = Isolate(current, visible, Crack);
                        if (cracks > peakCracks)
                        {
                            peakCracks = cracks;
                            NativeBattleRestorationPresentationTests.Publish(output, $"ground-crack-visible-peak-{current.NativeGraphics.Callback}", current, visible);
                        }
                    }
                }
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0)
                {
                    break;
                }
            }
            Assert.True(ticks < item.MaximumTicks);
            Assert.Empty(pending);
            foreach (short phase in new short[] { 1, 2, 3, 4, 5, 6, 7 })
            {
                Assert.Contains(phase, groundStates);
            }
            foreach (uint callback in new uint[] { 0x8DA900, 0x8DB030, 0x8DB410, 0x8DB630 })
            {
                Assert.Contains(callback, particles);
            }
            Assert.True(groundHidden);
            Assert.True(bounced);
            Assert.True(cameraChanged);
            Assert.True(peak > 0);
            Assert.True(peakCracks > 0);
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == installed.Quake38Resources!.RumbleSound);
            Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == installed.Quake38Resources!.QuakeSound);
            NativeBattleQuake38State final = battle.NativeEffectMemory!.Quake38!;
            Assert.Equal(7, final.Word(0x279299E));
            Assert.Equal(originalGround.Flags & 2, battle.Capture().NativeStage!.Groups[1].Flags & 2);
            Assert.Equal(0, final.Word(0x279299C));
            Assert.Equal(0, final.Word(0x2792D6C));
            Assert.Equal(0, final.Word(0x2792E18));
            Assert.Equal(0, final.Word(0x2792D6A));
            Assert.All(Selected(battle.Capture())!.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == caster);
            ColdNext("completed", battle.Capture());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "checked", item.Id, item.Effect, action.ActionId, Ticks = ticks, ContactReceipts = contacts, Completions = completions, GroundStates = groundStates.Order().ToArray(), ParticleCallbacks = particles.Order().ToArray(), PeakChangedPixels = peak, SeparateGroundCrackPixels = peakCracks, CameraChanged = cameraChanged, GroundHidden = groundHidden, TargetBounced = bounced }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            }
            throw;
        }

        int Isolate(BattleSnapshot snapshot, byte[] visible, Func<NativeBattleLogicalResolvedCommand, bool> selected)
        {
            NativeBattlePrimitiveFrame frame = snapshot.NativeGraphics!.PrimitiveFrame!;
            BattleSnapshot without = snapshot with { NativeGraphics = snapshot.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !selected(command)).ToImmutableArray() } } };
            return DifferentRgbPixels(visible, scene.Render(BattleSession.Restore(without), 320, 224));
        }
        NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { } effect && effect.Binding.Effect == item.Effect ? effect : null;
        void ColdNext(string name, BattleSnapshot snapshot, bool advanceOriginal = true)
        {
            BattleSession running = advanceOriginal ? battle : BattleSession.Restore(snapshot);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-before", snapshot, scene.Render(running, 320, 224));
            BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(scene.Render(running, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            running.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(running.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = scene.Render(running, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output, name + "-after", running.Capture(), pixels);
        }
    }

    private static NativeBattleModulePoolTask? Emitter(NativeBattleOriginalEffectState? effect) => effect?.Pools.SelectMany(pool => pool.Slots).SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x8D8720);
    private static int Stock(BattleSnapshot snapshot, string actor, ushort action) => snapshot.Combatants.Single(item => item.Id == actor).NativeMagicInventory!.Single(stock => stock.SpellId == action).Count;
    private static int DifferentRgbPixels(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int index = 0; index < first.Length; index += 4)
        {
            if (first[index] != second[index] || first[index + 1] != second[index + 1] || first[index + 2] != second[index + 2])
            {
                changed++;
            }
        }
        return changed;
    }
}
