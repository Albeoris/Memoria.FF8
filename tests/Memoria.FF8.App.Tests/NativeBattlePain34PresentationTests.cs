using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePain34PresentationTests
{
    [Fact]
    public void Original_Pain_runs_both_current_queue_players_status_contact_sound_pixels_and_independent_standalones()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_PAIN34_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "A distinct empty external Pain output directory is required.");
        Assert.False(Directory.Exists(output!) && Directory.EnumerateFileSystemEntries(output!).Any());
        Directory.CreateDirectory(output!);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.MagicId == 34);
        Assert.Equal((ushort)45, action.ActionId);
        string? supplied = Environment.GetEnvironmentVariable("MEMORIA_FF8_PAIN34_FIXTURE");
        string fixture = string.IsNullOrWhiteSpace(supplied) ? Path.Combine(output!, "fixture") : supplied;
        if (string.IsNullOrWhiteSpace(supplied))
        {
            NativeEffectFixtureRequest request = new("pain34", 34, 2, action.ActionId, 3, 800, 240) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 600 };
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixture);
            File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported, new JsonSerializerOptions { WriteIndented = true }));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        }
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "before-ordinary-input.battle.json"));
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "ordinary-produced-action104.battle.json"));
        string caster = source.NativeGraphics!.ActorIds[0]!;
        string target = source.NativeGraphics.ActorIds[3]!;
        int queued = Assert.Single(source.NativeGraphics.Descriptors.Queue, index => source.NativeGraphics.Descriptors.Packets[index].Command == 104);
        uint relative = source.NativeGraphics.Descriptors.Packets[queued].Argument - 0x1D280C4u;
        Assert.Equal(0u, relative % 20);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(relative / 20))];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal((ushort)45, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)34, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((ushort)26, contact.Primary.SavedStatuses4);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        Assert.Equal(2, Stock(beforeInput));
        Assert.Equal(1, Stock(source));
        int hp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
        uint projected = source.Definition.NativeGraphics!.Rules.Project(contact.Primary.SavedStatuses4, contact.Primary.BattleStatuses8);
        Assert.NotEqual(0u, projected);
        Assert.Equal(0u, source.NativeGraphics.Models[3].Flags8!.Value & projected);
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        BattleSnapshot prior = source;
        HashSet<string> restored = [];
        HashSet<uint> players = [];
        int ticks = 0;
        int contacts = 0;
        int completions = 0;
        int peakRgb = 0;
        bool privatePackets = false;
        bool observedDrawMode = false;
        try
        {
            ColdNext("produced");
            for (; ticks < 800; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                NativeBattleModulePoolTask? emitter = before?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x8658A0);
                string? boundary = before?.ReturnedDescriptor is > 0 && !restored.Contains("initialized") ? "initialized" : emitter is not null && emitter.ReadWord(36) is >= 12 and < 20 && !restored.Contains("live") ? "live" : emitter is not null && emitter.ReadWord(36) is >= 16 and < 20 && !restored.Contains("before-release") ? "before-release" : contacts > 0 && !restored.Contains("contact") ? "contact" : null;
                if (boundary is null)
                {
                    battle.Tick();
                }
                else
                {
                    ColdNext(boundary);
                }
                BattleSnapshot current = battle.Capture();
                Assert.Equal(1, Stock(current));
                Assert.Equal(hp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                NativeBattleOriginalEffectState? after = Selected(current);
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                uint oldStatus = prior.NativeGraphics!.Models[3].Flags8!.Value & projected;
                uint newStatus = current.NativeGraphics!.Models[3].Flags8!.Value & projected;
                if (oldStatus == 0 && newStatus != 0)
                {
                    contacts++;
                    Assert.Equal(projected, newStatus);
                }
                foreach (NativeBattleModulePoolTask player in after?.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0 && task.Callback is 0x8659D0 or 0x865DB0) ?? [])
                {
                    players.Add(player.Callback);
                }
                NativeBattlePrimitiveFrame primitive = current.NativeGraphics.PrimitiveFrame!;
                bool hasPrivate = primitive.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                privatePackets |= hasPrivate;
                observedDrawMode |= current.NativeEffectMemory?.PrimitiveProcess?.DrawMode2795BD0 == 0xE1000220;
                if (hasPrivate)
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame removed = primitive with { LastCommands = primitive.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession without = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = removed } });
                    int changed = DifferentRgb(visible, scene.Render(without, 320, 224).ToArray());
                    if (changed > peakRgb)
                    {
                        peakRgb = changed;
                        NativeBattleRestorationPresentationTests.Publish(output!, "visible-" + current.NativeGraphics.Callback, current, visible);
                    }
                }
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104))
                {
                    break;
                }
            }
            Assert.True(ticks < 800, "Pain exceeded its bounded callback budget.");
            Assert.Equal(new uint[] { 0x8659D0, 0x865DB0 }, players.Order().ToArray());
            Assert.True(privatePackets);
            Assert.True(observedDrawMode);
            Assert.True(peakRgb > 0, "Removing only actual private Pain packets must change visible RGB pixels.");
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.Equal(projected, battle.Capture().NativeGraphics!.Models[3].Flags8!.Value & projected);
            Assert.All(Selected(battle.Capture())!.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
            Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == source.Definition.NativeEffectCatalog[33].Pain34Resources!.Sound);
            ColdNext("completed");
            Assert.Contains("initialized", restored);
            Assert.Contains("live", restored);
            Assert.Contains("before-release", restored);
            Assert.Contains("contact", restored);
            Report("checked", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-failure", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "at-failure", battle.Capture(), null);
            Report("blocked", error.ToString());
            throw;
        }
        int Stock(BattleSnapshot snapshot) => snapshot.Combatants.Single(actor => actor.Id == caster).NativeMagicInventory!.Single(stock => stock.SpellId == action.ActionId).Count;
        static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 34 } effect ? effect : null;
        void ColdNext(string name)
        {
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-before", snapshot, scene.Render(battle, 320, 224));
            BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + "-before.battle.json")));
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            battle.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = scene.Render(battle, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-after", battle.Capture(), pixels);
            restored.Add(name);
        }
        void Report(string status, string? blocker) => File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Effect = 34, action.ActionId, Ticks = ticks, Contacts = contacts, Completions = completions, PrivateRgbPixels = peakRgb, PrivatePackets = privatePackets, DrawModeObserved = observedDrawMode, Players = players.Order().ToArray(), Restored = restored.Order().ToArray(), Scope = "Declared original ordinary Magic row45; no story travel or original framebuffer differential claim" }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static int DifferentRgb(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int count = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (first[offset] != second[offset] || first[offset + 1] != second[offset + 1] || first[offset + 2] != second[offset + 2])
            {
                count++;
            }
        }
        return count;
    }
}