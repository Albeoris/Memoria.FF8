using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDispel109PresentationTests
{
    [Fact]
    public void Original_stock_Dispel_renders_models_and_sprites_consumes_contact_and_restores_independent_disk_boundaries()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DISPEL109_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "An external empty Dispel output directory is required.");
        Assert.False(Directory.Exists(output!) && Directory.EnumerateFileSystemEntries(output!).Any());
        Directory.CreateDirectory(output!);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.MagicId == 109);
        Assert.Equal((ushort)28, action.ActionId);
        Assert.Equal(1, action.KernelSection);
        Assert.Equal(28, action.KernelIndex);
        Assert.Equal((byte)3, action.AttackType);
        string? supplied = Environment.GetEnvironmentVariable("MEMORIA_FF8_DISPEL109_FIXTURE");
        string fixture = string.IsNullOrWhiteSpace(supplied) ? Path.Combine(output!, "fixture") : supplied;
        if (string.IsNullOrWhiteSpace(supplied))
        {
            NativeEffectFixtureRequest request = new("dispel109", 109, 2, action.ActionId, 3, 800, 180) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 600 };
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixture);
            File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported, new JsonSerializerOptions { WriteIndented = true }));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        }
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "before-ordinary-input.battle.json"));
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "ordinary-produced-action104.battle.json"));
        string caster = source.NativeGraphics!.ActorIds[0]!;
        string target = source.NativeGraphics.ActorIds[3]!;
        int descriptor = Assert.Single(source.NativeGraphics.Descriptors.Queue, index => source.NativeGraphics.Descriptors.Packets[index].Command == 104);
        uint relative = source.NativeGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u;
        Assert.Equal(0u, relative % 20);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(relative / 20))];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)109, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        Assert.Equal(2, Stock(beforeInput));
        Assert.Equal(1, Stock(source));
        int hp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
        Assert.Equal(beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp, hp);
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> restored = [];
        Dictionary<string, int> peaks = [];
        int contacts = 0;
        int completions = 0;
        int ticks = 0;
        int ringPeak = 0;
        int spritePeak = 0;
        bool sound = false;
        BattleSnapshot prior = source;
        try
        {
            ColdNext("produced");
            for (; ticks < 500; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                int age = TargetAge(prior);
                string? boundary = before?.ReturnedDescriptor is > 0 && !restored.Contains("initialized") ? "initialized"
                    : age is >= 23 and <= 25 && !restored.Contains("swirls") ? "swirls"
                    : age is >= 28 and <= 30 && !restored.Contains("random-sparks") ? "random-sparks"
                    : age is >= 65 and <= 67 && !restored.Contains("pre-contact") ? "pre-contact" : null;
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
                if (!prior.NativeGraphics!.Effects.Slots.Any(ContactText) && current.NativeGraphics!.Effects.Slots.Any(ContactText))
                {
                    Assert.InRange(67 - age, 0L, Math.Max(0L, current.NativeGraphics.Callback - prior.NativeGraphics.Callback - 1));
                    contacts++;
                    Assert.Equal(contact.Primary.Sequence, current.NativeGraphics.Models[3].Sequence);
                }
                NativeBattleOriginalEffectState? after = Selected(current);
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                if (after?.ReturnedDescriptor is > 0)
                {
                    Assert.Equal(0, after.Dispel109!.BufferOffset);
                    Assert.True(current.NativeEffectMemory!.Cursor > 0);
                }
                NativeBattlePrimitiveFrame primitive = current.NativeGraphics!.PrimitiveFrame!;
                if (primitive.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame without = primitive with { LastCommands = primitive.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    int changed = DifferentRgb(visible, scene.Render(plain, 320, 224).ToArray());
                    string? phase = age is >= 0 and < 22 ? "rings" : age is >= 47 and <= 54 ? "sparkles" : null;
                    if (phase is not null && changed > 0)
                    {
                        if (!peaks.ContainsKey(phase))
                        {
                            NativeBattleRestorationPresentationTests.Publish(output!, "visible-" + phase, current, visible);
                        }
                        peaks[phase] = Math.Max(peaks.GetValueOrDefault(phase), changed);
                    }
                    if (phase == "rings")
                    {
                        ringPeak = Math.Max(ringPeak, changed);
                    }
                    if (phase == "sparkles")
                    {
                        spritePeak = Math.Max(spritePeak, changed);
                    }
                }
                sound |= battle.Events.Any(item => item.NativeModuleSound?.Sound == source.Definition.NativeEffectCatalog[108].Dispel109Resources!.Sound);
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104))
                {
                    break;
                }
            }
            Assert.True(ticks < 500, "Dispel exceeded the declared diagnostic callback budget.");
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.True(sound);
            Assert.True(ringPeak > 0);
            Assert.True(spritePeak > 0);
            Assert.All(Selected(battle.Capture())!.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
            Assert.All(battle.Capture().NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            ColdNext("completed");
            Assert.Equal(new[] { "completed", "initialized", "pre-contact", "produced", "random-sparks", "swirls" }, restored.Order().ToArray());
            Report("checked", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-failure", prior, null);
            Report("blocked", error.ToString());
            throw;
        }

        bool ContactText(NativeBattleEffectTask task) => (task.Flags0 & 1) != 0 && task.ModelIndex == 3 && task.Kind == NativeBattleEffectKind.DamageText5069B0;
        int Stock(BattleSnapshot snapshot) => snapshot.Combatants.Single(actor => actor.Id == caster).NativeMagicInventory!.Single(stock => stock.SpellId == action.ActionId).Count;
        static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 109 } effect ? effect : null;
        static int TargetAge(BattleSnapshot snapshot) => Selected(snapshot)?.Pools is { Length: > 1 } pools && pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0) is { } target ? unchecked((short)target.ReadWord(12)) : -1;
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
        void Report(string status, string? blocker) => File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Effect = 109, action.ActionId, Ticks = ticks, Contacts = contacts, Completions = completions, RingChangedRgbPixels = ringPeak, SpriteChangedRgbPixels = spritePeak, Peaks = peaks, Restored = restored.Order().ToArray(), ContactFlags = contact.Primary.ContactFlags, ContactSequence = contact.Primary.Sequence, Scope = "Original command2 row28 declared-stock component; clean target exercises native removal attempt/miss presentation, not positive-status removal or story-route proof" }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static int DifferentRgb(byte[] first, byte[] second)
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
