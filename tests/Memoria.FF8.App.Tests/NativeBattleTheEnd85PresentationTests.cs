using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTheEnd85PresentationTests
{
    [Fact]
    public void Original_declared_stock_The_End_runs_all_stages_applies_one_KO_contact_and_restores_through_independent_disk_boundaries()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_THEEND85_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "An external empty The End output directory is required.");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        Assert.False(Directory.Exists(output!) && Directory.EnumerateFileSystemEntries(output!).Any());
        Directory.CreateDirectory(output!);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.MagicId == 85);
        Assert.Equal((ushort)56, action.ActionId);
        Assert.Equal(1, action.KernelSection);
        Assert.Equal(56, action.KernelIndex);
        string? supplied = Environment.GetEnvironmentVariable("MEMORIA_FF8_THEEND85_FIXTURE");
        string fixture = string.IsNullOrWhiteSpace(supplied) ? Path.Combine(output!, "fixture") : supplied;
        if (string.IsNullOrWhiteSpace(supplied))
        {
            NativeEffectFixtureRequest request = new("theend85", 85, 2, action.ActionId, 3, 800, 240) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 600 };
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
        Assert.Equal((ushort)85, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.NotEqual(0, contact.Primary.SavedStatuses4 & 1);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        Assert.Equal(2, Stock(beforeInput));
        Assert.Equal(1, Stock(source));
        Assert.True(beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp > 0);
        Assert.Equal(0, source.Combatants.Single(actor => actor.Id == target).CurrentHp);
        uint projected = source.Definition.NativeGraphics!.Rules.Project(contact.Primary.SavedStatuses4, contact.Primary.BattleStatuses8);
        Assert.NotEqual(0u, projected);
        Assert.Equal(0u, source.NativeGraphics.Models[3].Flags8!.Value & projected);
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> restored = [];
        HashSet<int> stages = [];
        HashSet<string> rgbPhases = [];
        Dictionary<string, int> peaks = [];
        Dictionary<string, object> evidence = [];
        int privatePeak = 0;
        int contacts = 0;
        int completions = 0;
        int ticks = 0;
        bool hiddenStage = false;
        bool privatePackets = false;
        bool commonPackets = false;
        BattleSnapshot prior = source;
        try
        {
            ColdNext("produced");
            for (; ticks < 1400; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                int stage = Stage(prior);
                string? boundary = before?.ReturnedDescriptor is > 0 && !restored.Contains("initialized") ? "initialized" : stage > 0 && !restored.Contains("stage-" + stage) ? "stage-" + stage : null;
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
                Assert.Equal(0, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                uint oldFlags = prior.NativeGraphics!.Models[3].Flags8!.Value & projected;
                uint newFlags = current.NativeGraphics!.Models[3].Flags8!.Value & projected;
                if (oldFlags == 0 && newFlags != 0)
                {
                    contacts++;
                }
                NativeBattleOriginalEffectState? after = Selected(current);
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                int currentStage = Stage(current);
                if (currentStage is > 0 and <= 6)
                {
                    stages.Add(currentStage);
                }
                hiddenStage |= current.NativeStage!.Groups.Any(group => (group.Flags & 2) == 0 && group.ColorDepth != 0);
                NativeBattlePrimitiveFrame primitive = current.NativeGraphics.PrimitiveFrame!;
                privatePackets |= primitive.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                commonPackets |= primitive.LastCommands.Any(CommonEffect);
                NativeBattleModulePoolTask? flower = after?.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0 && task.Callback == 0x788A90 && task.ReadDword(116) == 0x1715D28 && (task.ReadByte(38) & 4) == 0 && task.ReadDword(152) >= 36).OrderByDescending(task => task.ReadDword(152)).FirstOrDefault();
                NativeBattleModulePoolTask? screenMesh = after?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x78C4C0 && task.ReadDword(76) == 0x156C8BC && task.ReadWord(224) == 108 && task.ReadWord(36) >= 21 && (task.ReadByte(38) & 4) == 0);
                NativeBattleModulePoolTask? footer = currentStage == 5 ? after?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x78D250 && task.ReadByte(41) == 2 && task.ReadWord(88) == 128 && task.ReadWord(90) == 128) : null;
                NativeBattleModulePoolTask? bars = currentStage == 5 ? after?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x78D410 && task.ReadByte(41) == 1 && task.ReadWord(30) == 24) : null;
                string? phase = flower is not null && !rgbPhases.Contains("flowers") ? "flowers" : screenMesh is not null && !rgbPhases.Contains("screen-mesh") ? "screen-mesh" : footer is not null && !rgbPhases.Contains("footer") ? "footer" : footer is not null && bars is not null && !rgbPhases.Contains("letterbox") ? "letterbox" : currentStage switch { 3 => "models", 4 => "secondary", _ => null };
                if (privatePeak == 0 && primitive.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    privatePeak = ChangedPixels(command => command.Command.Packet.Bank == "20DFAB8");
                }
                if (phase is not null && !rgbPhases.Contains(phase))
                {
                    Func<NativeBattleLogicalResolvedCommand, bool> include = phase switch
                    {
                        "flowers" => command => CommonEffect(command) && (command.Command.Opcode & 0xFC) is 0x30 or 0x38,
                        "screen-mesh" => command => CommonEffect(command) && (command.Command.Opcode & 0xFC) == 0x3C,
                        "footer" => FooterPacket,
                        "letterbox" => LetterboxPacket,
                        _ => CommonEffect
                    };
                    NativeBattleLogicalResolvedCommand[] matched = primitive.LastCommands.Where(include).ToArray();
                    if (matched.Length != 0)
                    {
                        int changed = ChangedPixels(include);
                        if (changed > 0)
                        {
                            rgbPhases.Add(phase);
                            peaks[phase] = changed;
                            NativeBattleModulePoolTask? witnessed = phase == "flowers" ? flower : phase == "screen-mesh" ? screenMesh : phase == "footer" ? footer : phase == "letterbox" ? bars : null;
                            object receipt = new { GraphicsCallback = current.NativeGraphics.Callback, DirectorStage = currentStage, TaskCallback = witnessed?.Callback, Source = phase == "flowers" ? 0x1715D28u : phase == "screen-mesh" ? 0x156C8BCu : (uint?)null, Age = witnessed?.ReadWord(36), AnimationWorkspaceFrame = phase == "flowers" ? witnessed!.ReadDword(152) : (uint?)null, ScreenThreshold = phase == "screen-mesh" ? witnessed!.ReadWord(224) : (ushort?)null, Opcodes = matched.Select(command => command.Command.Opcode).Distinct().Order().ToArray(), Packets = matched.Length, ChangedRgbPixels = changed, Attribution = phase == "flowers" ? "Live1715D28 flower animation atframe>=36 and original G3/G4 packets; private sparkle sprites excluded" : phase == "screen-mesh" ? "Live156C8BC screen/ground mesh at settledthreshold108 and original GT4 packets; no lettering interpretation" : phase == "footer" ? "Live78D250 phase2 with WORD88/90=128; exact bucket5 GT4 rectangle186,160..314,192, CLUT15700/page182" : phase == "letterbox" ? "Live78D410 phase1 height24; exact bucket5 black F4 top0..24 and bottom192..216" : "Original regional COMMON packets during this director stage" };
                            evidence[phase] = receipt;
                            File.WriteAllText(Path.Combine(output!, "visible-" + phase + "-receipt.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
                            NativeBattleRestorationPresentationTests.Publish(output!, "visible-" + phase, current, scene.Render(battle, 320, 224).ToArray());
                        }
                    }
                }

                int ChangedPixels(Func<NativeBattleLogicalResolvedCommand, bool> include)
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame without = primitive with { LastCommands = primitive.LastCommands.Where(command => !include(command)).ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    return DifferentRgb(visible, scene.Render(plain, 320, 224).ToArray());
                }

                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104))
                {
                    break;
                }
            }
            Assert.True(ticks < 1400, "The End exceeded its declared callback budget.");
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, stages.Order().ToArray());
            Assert.Equal(new[] { "flowers", "footer", "letterbox", "models", "screen-mesh", "secondary" }, rgbPhases.Order().ToArray());
            Assert.True(privatePackets);
            Assert.True(privatePeak > 0);
            Assert.True(commonPackets);
            Assert.True(hiddenStage);
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            BattleSnapshot finished = battle.Capture();
            for (int actor = 0; actor < 3; actor++)
            {
                Assert.Equal(source.NativeGraphics.Models[actor].Flags0 & 4, finished.NativeGraphics!.Models[actor].Flags0 & 4);
            }
            Assert.Equal(source.NativeStage!.Groups.Select(group => group.Flags & 2), finished.NativeStage!.Groups.Select(group => group.Flags & 2));
            Assert.All(Selected(battle.Capture())!.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
            foreach (uint sound in source.Definition.NativeEffectCatalog[84].TheEnd85Resources!.Sounds.Values)
            {
                Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == sound);
            }
            ColdNext("completed");
            Report("checked", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-failure", prior, null);
            Report("blocked", error.ToString());
            throw;
        }
        int Stock(BattleSnapshot snapshot) => snapshot.Combatants.Single(actor => actor.Id == caster).NativeMagicInventory!.Single(stock => stock.SpellId == action.ActionId).Count;
        static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 85 } effect ? effect : null;
        static bool CommonEffect(NativeBattleLogicalResolvedCommand command) => command.Command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.Command.OrderingBucket is >= 17 and < 4112;
        static bool FooterPacket(NativeBattleLogicalResolvedCommand command)
        {
            ImmutableArray<uint> words = command.Command.Words;
            return command.Command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.Command.OrderingBucket == 5 && command.Command.Opcode == 0x3E && words.Length == 12 && words[1] == 0x00A000BA && words[4] == 0x00A0013A && words[7] == 0x00C000BA && words[10] == 0x00C0013A && words[2] >> 16 == 15700 && words[5] >> 16 == 182;
        }
        static bool LetterboxPacket(NativeBattleLogicalResolvedCommand command)
        {
            ImmutableArray<uint> words = command.Command.Words;
            return command.Command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.Command.OrderingBucket == 5 && command.Command.Opcode == 0x29 && words.Length == 5 && (words[0] & 0xFFFFFF) == 0 && (words[1] == 0 && words[2] == 320 && words[3] == 0x00180000 && words[4] == 0x00180140 || words[1] == 0x00C00000 && words[2] == 0x00C00140 && words[3] == 0x00D80000 && words[4] == 0x00D80140);
        }
        static int Stage(BattleSnapshot snapshot) => snapshot.NativeEffectMemory?.TheEnd85 is { } state ? state.Word(0x25C1C60 + 64) : 0;
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
        void Report(string status, string? blocker) => File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Effect = 85, action.ActionId, Ticks = ticks, Contacts = contacts, Completions = completions, Stages = stages.Order().ToArray(), RgbPhases = rgbPhases.Order().ToArray(), Peaks = peaks, PrivateChangedRgbPixels = privatePeak, PhaseEvidence = evidence, Restored = restored.Order().ToArray(), Scope = "Declared-stock original command2 row56 component, not a Slot UI or story route claim" }, new JsonSerializerOptions { WriteIndented = true }));
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
