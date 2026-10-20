using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWall189PresentationTests
{
    [Fact]
    public void Original_Wall_completes_target_status_capture_particles_sound_texture_restore_and_standalone_next()
    {
        string? manifest = Environment.GetEnvironmentVariable("MEMORIA_FF8_WALL_CASES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifest), "An external ordinary Magic case manifest is required.");
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_WALL_CASE") ?? throw new InvalidDataException("One external case identity is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_WALL_OUTPUT") ?? throw new InvalidDataException("A distinct external case output directory is required.");
        NativeBattleOrdinaryMagicCase item = (JsonSerializer.Deserialize<NativeBattleOrdinaryMagicCases>(File.ReadAllBytes(manifest!)) ?? throw new InvalidDataException("The case manifest is null.")).Cases.Single(item => item.Id == id);
        Assert.Equal((ushort)189, item.Effect);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.MagicId == item.Effect && (item.ActionId is null || action.ActionId == item.ActionId));
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([item.Effect])[item.Effect - 1];
        string? supplied = Environment.GetEnvironmentVariable("MEMORIA_FF8_WALL_SNAPSHOT");
        string sourcePath;
        if (string.IsNullOrWhiteSpace(supplied))
        {
            NativeEffectFixtureRequest request = new(item.Id, item.Effect, 2, action.ActionId, 0, item.EntryMaximumCallbacks, item.ProcessTimeoutSeconds) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = item.ProducerMaximumCallbacks };
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
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        Assert.NotEqual(0u, (uint)action.Statuses0 | action.Statuses1);
        Assert.Equal(action.Statuses0, (ushort)(contact.Primary.SavedStatuses4 & action.Statuses0));
        Assert.Equal(action.Statuses1, contact.Primary.BattleStatuses8 & action.Statuses1);
        string target = source.NativeGraphics!.ActorIds[0]!;
        string caster = source.NativeGraphics.ActorIds[0]!;
        Assert.Equal(2, Stock(beforeInput, caster, action.ActionId));
        Assert.Equal(1, Stock(source, caster, action.ActionId));
        int producedHp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
        Assert.Equal(beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp, producedHp);
        uint statusMask = action.Statuses1;
        Assert.NotEqual(0u, statusMask);
        Assert.Equal(0u, source.Definition.NativeGraphics!.Rules.Project(action.Statuses0, action.Statuses1));
        Assert.Equal(0u, source.NativePartyStatusMirrors!.Battle[0]!.Value & statusMask);
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> pending = ["initialized", "shield"];
        int receipts = 0;
        int completions = 0;
        int peak = 0;
        int peakShield = 0;
        bool cameraChanged = false;
        int ticks = 0;
        BattleSnapshot? prior = null;
        try
        {
            ColdNext("produced", source, false);
            for (; ticks < item.MaximumTicks; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                NativeBattleModulePoolTask? previousTarget = Target(before);
                int age = previousTarget is null ? -1 : unchecked((short)previousTarget.ReadWord(12));
                string? boundary = before?.ReturnedDescriptor is > 0 && pending.Contains("initialized") ? "initialized" : age >= 50 && pending.Contains("shield") ? "shield" : null;
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
                NativeBattleModulePoolTask? currentTarget = Target(after);
                int nextAge = currentTarget is null ? -1 : unchecked((short)currentTarget.ReadWord(12));
                Assert.Equal(producedHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(1, Stock(current, caster, action.ActionId));
                cameraChanged |= JsonSerializer.Serialize(current.NativeCamera?.Projection) != JsonSerializer.Serialize(source.NativeCamera?.Projection);
                if (age == 65 && nextAge == 66)
                {
                    Assert.Equal(0u, prior.NativePartyStatusMirrors!.Battle[0]!.Value & statusMask);
                    Assert.Equal(statusMask, current.NativePartyStatusMirrors!.Battle[0]!.Value & statusMask);
                    receipts++;
                    File.WriteAllText(Path.Combine(output, "generator65-contact-receipt.json"), JsonSerializer.Serialize(new { BeforeCallback = prior.NativeGraphics!.Callback, AfterCallback = current.NativeGraphics!.Callback, BeforeStatus = prior.NativePartyStatusMirrors!.Battle[0], AfterStatus = current.NativePartyStatusMirrors!.Battle[0], Contact = contact, Observation = "Original Wall generator65 consumes original target packets once through506BA0/506690; native493840/47E2D0 party status mirror receives original Protect/Shell bits; authenticated graphics projection has no mapping for these bits" }, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                NativeBattlePrimitiveFrame frame = current.NativeGraphics!.PrimitiveFrame!;
                if (frame.LastCommands.Any(command => command.Command.Packet.Bank == "2296A30"))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame without = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "2296A30").ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    int changed = DifferentRgbPixels(visible, scene.Render(plain, 320, 224));
                    if (changed > peak)
                    {
                        peak = changed;
                        NativeBattleRestorationPresentationTests.Publish(output, $"visible-peak-{current.NativeGraphics.Callback}", current, visible);
                    }
                }
                HashSet<NativeBattlePrimitiveReference> shieldPackets = frame.LastCommands.Where(command => command.Command.Packet.Bank == "2296A30" && command.Command.Opcode == 0xE1 && command.Command.Words[0] == 0xE1000020u).Select(command => command.Command.Packet).ToHashSet();
                shieldPackets.IntersectWith(frame.LastCommands.Where(command => command.Command.Packet.Bank == "2296A30" && command.Command.Opcode == 0xE1 && command.Command.Words[0] == 0xE1000220u).Select(command => command.Command.Packet));
                bool IsShield(NativeBattleLogicalResolvedCommand command) => shieldPackets.Contains(command.Command.Packet);
                if (frame.LastCommands.Any(IsShield))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame without = frame with { LastCommands = frame.LastCommands.Where(command => !IsShield(command)).ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    int changed = DifferentRgbPixels(visible, scene.Render(plain, 320, 224));
                    if (changed > peakShield)
                    {
                        peakShield = changed;
                        NativeBattleRestorationPresentationTests.Publish(output, $"captured-shield-visible-peak-{current.NativeGraphics.Callback}", current, visible);
                    }
                }
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0)
                {
                    break;
                }
            }
            Assert.True(ticks < item.MaximumTicks);
            Assert.Empty(pending);
            Assert.True(cameraChanged);
            Assert.Equal(1, receipts);
            Assert.Equal(1, completions);
            Assert.True(peak > 0);
            Assert.True(peakShield > 0);
            Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == installed.Wall189Resources!.Sound);
            NativeBattleWall189State finished = battle.NativeEffectMemory!.Wall189!;
            Assert.Equal(source.Definition.NativeCommonEffectTexture!.Data.ToArray(), finished.Packets.Read(0, source.Definition.NativeCommonEffectTexture.Data.Length));
            Assert.All(Enumerable.Range(0, 150), index => Assert.Equal(0u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(finished.Sprites.AsSpan().Slice(index * 24))));
            Assert.All(Enumerable.Range(0, 100), index => Assert.Equal(0u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(finished.Meshes.AsSpan().Slice(index * 24))));
            Assert.All(Selected(battle.Capture())!.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == caster);
            ColdNext("completed", battle.Capture());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "checked", item.Id, item.Effect, action.ActionId, Ticks = ticks, ContactReceipts = receipts, PeakChangedPixels = peak, CapturedShieldPixels = peakShield, CameraChanged = cameraChanged, Completions = completions, OriginalSound = installed.Wall189Resources!.Sound }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            }
            throw;
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

    private static NativeBattleModulePoolTask? Target(NativeBattleOriginalEffectState? effect) => effect?.Pools.SelectMany(pool => pool.Slots).SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x5BCA20);
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
