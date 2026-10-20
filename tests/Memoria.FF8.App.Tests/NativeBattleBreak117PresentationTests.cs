using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBreak117PresentationTests
{
    [Fact]
    public void Original_Break_completes_saved_status_shell_stone_pixels_sound_and_standalone_next()
    {
        string? manifest = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREAK117_CASES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifest), "An external ordinary Magic case manifest is required.");
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREAK117_CASE") ?? throw new InvalidDataException("One external case identity per process is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREAK117_OUTPUT") ?? throw new InvalidDataException("A distinct external case output directory is required.");
        NativeBattleOrdinaryMagicCase item = (JsonSerializer.Deserialize<NativeBattleOrdinaryMagicCases>(File.ReadAllBytes(manifest!)) ?? throw new InvalidDataException("The ordinary case manifest is null.")).Cases.Single(item => item.Id == id);
        Assert.Equal((ushort)117, item.Effect);
        Assert.True(item.EntryMaximumCallbacks > 0 && item.ProducerMaximumCallbacks > 0 && item.MaximumTicks > 0 && item.ProcessTimeoutSeconds > 0);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(), "Output must be a distinct empty directory.");
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction[] candidates = assets.NativeActions.Where(action => action.Command == 2 && action.MagicId == item.Effect && (item.ActionId is null || action.ActionId == item.ActionId)).ToArray();
        if (candidates.Length != 1)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", item.Id, item.Effect, FirstBlocker = $"Expected one exact installed command2/effect row, found{candidates.Length}. No alias selection was made." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        BattleNativeAction action = Assert.Single(candidates);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([item.Effect])[item.Effect - 1];
        int ticks = 0;
        int completions = 0;
        int contactSequenceTransitions = 0;
        int statusTransitions = 0;
        int contactAgeCrossings = 0;
        int graphicsStatusTransitions = 0;
        int peakChangedPixels = 0;
        int peakStoneChangedPixels = 0;
        string contactObserver = "pending";
        long sourceCallback = -1;
        long firstLiveCallback = -1;
        bool initialized = false;
        bool observedPrivatePackets = false;
        bool observedAdditivePackets = false;
        HashSet<string> pending = ["initialized", "live-continuation", "stone-body"];
        List<string> restoredBoundaries = [];
        BattleSession? battle = null;
        BattleSnapshot? prior = null;
        NativeEffectFixtureExportResult? exported = null;
        string inputScenario = "Fresh declared component through the ordinary Magic producer";
        string? sourceSha256 = null;
        try
        {
            string? suppliedSnapshot = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREAK117_SNAPSHOT");
            string sourcePath;
            if (!string.IsNullOrWhiteSpace(suppliedSnapshot))
            {
                sourcePath = suppliedSnapshot;
                inputScenario = "Existing complete ordinary-produced header and sibling pre-input standalone; no export or hydration";
            }
            else
            {
                NativeEffectFixtureRequest request = new(item.Id, item.Effect, 2, action.ActionId, 3, item.EntryMaximumCallbacks, item.ProcessTimeoutSeconds) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = item.ProducerMaximumCallbacks };
                string fixtureDirectory = Path.Combine(output, "fixture");
                exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixtureDirectory);
                File.WriteAllText(Path.Combine(output, "fixture-result.json"), JsonSerializer.Serialize(exported, new JsonSerializerOptions { WriteIndented = true }));
                Assert.True(exported.Status != "blocked", exported.FirstBlocker);
                sourcePath = Path.Combine(fixtureDirectory, "ordinary-produced-action104.battle.json");
            }
            BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath);
            BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, "before-ordinary-input.battle.json"));
            sourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)));
            prior = source;
            NativeBattleGraphicsState graphics = source.NativeGraphics!;
            sourceCallback = graphics.Callback;
            int slot = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
            uint offset = unchecked(graphics.Descriptors.Packets[slot].Argument - 0x1D280C4u);
            Assert.True(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
            int headerIndex = checked((int)(offset / 20));
            NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[headerIndex];
            Assert.Equal((byte)2, header.ReadByte(1));
            Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
            Assert.Equal(item.Effect, NativeBattleActionGraphics.Word(header, 6));
            Assert.True(graphics.ActionGraphics is null || graphics.ActionGraphics.HeaderIndex == headerIndex);
            Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[item.Effect - 1]));
            NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("The ordinary producer omitted its contact owner."));
            Assert.Equal((byte)3, contact.Primary.ActorIndex);
            Assert.Equal((byte)255, contact.Secondary.ActorIndex);
            Assert.NotEqual((ushort)0, action.Statuses0);
            Assert.Equal(0u, action.Statuses1);
            Assert.Equal(action.Statuses0, (ushort)(contact.Primary.SavedStatuses4 & action.Statuses0));
            Assert.Equal(0u, contact.Primary.BattleStatuses8 & action.Statuses1);
            Assert.Equal((ushort)0, contact.Primary.Amount6);
            string target = graphics.ActorIds[3]!;
            string caster = graphics.ActorIds[0]!;
            int beforeHp = beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp;
            int targetHp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
            Assert.Equal(beforeHp, targetHp);
            ushort statusMask = action.Statuses0;
            uint graphicsStatusMask = source.Definition.NativeGraphics!.Rules.Project(action.Statuses0, action.Statuses1);
            Assert.NotEqual(0u, graphicsStatusMask);
            Assert.Equal(0u, source.NativeGraphics!.Models[3].Flags8!.Value & graphicsStatusMask);
            Assert.Equal(0, beforeInput.Combatants.Single(actor => actor.Id == target).NativeStatus!.Saved & statusMask);
            NativeBattleStatusSnapshot producedStatus = source.Combatants.Single(actor => actor.Id == target).NativeStatus!;
            Assert.Equal(statusMask, producedStatus.Saved & statusMask);
            Assert.Equal(statusMask, producedStatus.Presentation.Saved!.Value & statusMask);
            Assert.Equal(2, Stock(beforeInput, caster, action.ActionId));
            Assert.Equal(1, Stock(source, caster, action.ActionId));
            contactObserver = "Native506690 publishes the installed saved-status projection to model Flags8 once at target78; actor status presentation is already produced";
            battle = BattleSession.Restore(source);
            Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
            BattleScenePresentation scene = new(assets, source.Definition, true);
            ColdNext("produced", source, scene, false);
            for (; ticks < item.MaximumTicks; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                string? boundary = null;
                if (before?.ReturnedDescriptor is > 0)
                {
                    if (pending.Contains("initialized"))
                    {
                        boundary = "initialized";
                        firstLiveCallback = prior.NativeGraphics!.Callback;
                    }
                    else if (pending.Contains("live-continuation") && prior.NativeGraphics!.Callback >= firstLiveCallback + 20)
                    {
                        boundary = "live-continuation";
                    }
                }
                if (before?.ReturnedDescriptor is > 0 && pending.Contains("stone-body") && TargetAge(before) >= 30)
                {
                    boundary = "stone-body";
                }
                if (boundary is not null)
                {
                    ColdNext(boundary, prior, scene);
                    pending.Remove(boundary);
                }
                else
                {
                    battle.Tick();
                }
                BattleSnapshot current = battle.Capture();
                Assert.Equal(targetHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(1, Stock(current, caster, action.ActionId));
                if (prior.NativeGraphics!.Models[3].Sequence != contact.Primary.Sequence && current.NativeGraphics!.Models[3].Sequence == contact.Primary.Sequence)
                {
                    contactSequenceTransitions++;
                }
                int priorStatus = prior.Combatants.Single(actor => actor.Id == target).NativeStatus!.Presentation.Saved!.Value & statusMask;
                int currentStatus = current.Combatants.Single(actor => actor.Id == target).NativeStatus!.Presentation.Saved!.Value & statusMask;
                Assert.Equal(statusMask, priorStatus);
                Assert.Equal(statusMask, currentStatus);
                if (currentStatus != priorStatus)
                {
                    statusTransitions++;
                }
                uint priorGraphicsStatus = prior.NativeGraphics!.Models[3].Flags8!.Value & graphicsStatusMask;
                uint currentGraphicsStatus = current.NativeGraphics!.Models[3].Flags8!.Value & graphicsStatusMask;
                if (priorGraphicsStatus != currentGraphicsStatus)
                {
                    Assert.Equal(0u, priorGraphicsStatus);
                    Assert.Equal(graphicsStatusMask, currentGraphicsStatus);
                    graphicsStatusTransitions++;
                }
                int previousTargetAge = TargetAge(Selected(prior));
                int currentTargetAge = TargetAge(Selected(current));
                if (previousTargetAge >= 0 && previousTargetAge <= 78 && currentTargetAge > 78)
                {
                    contactAgeCrossings++;
                    Assert.Equal(78, previousTargetAge);
                    Assert.Equal(79, currentTargetAge);
                    Assert.Equal(0u, priorGraphicsStatus);
                    Assert.Equal(graphicsStatusMask, currentGraphicsStatus);
                    File.WriteAllText(Path.Combine(output, "target78-contact-receipt.json"), JsonSerializer.Serialize(new { BeforeCallback = prior.NativeGraphics!.Callback, AfterCallback = current.NativeGraphics!.Callback, BeforeAge = previousTargetAge, AfterAge = currentTargetAge, BeforeFlags = priorGraphicsStatus, AfterFlags = currentGraphicsStatus, InstalledGraphicsStatusMask = graphicsStatusMask, Contact = contact, Observation = "Actual saved model Flags8 native status projection changes once at the original target78 contact; no task/contact/status mutation" }, new JsonSerializerOptions { WriteIndented = true }));
                }
                NativeBattleOriginalEffectState? after = Selected(current);
                if (after?.ReturnedDescriptor is > 0)
                {
                    Assert.True(current.NativeEffectMemory!.Cursor > 0);
                    Assert.Equal(0, after.Break117!.BufferOffset);
                }
                initialized |= after?.ReturnedDescriptor is > 0;
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                observedPrivatePackets |= current.NativeGraphics!.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                bool additive = frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0xE1);
                observedAdditivePackets |= additive;
                if (after?.ReturnedDescriptor is > 0 && frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame withoutEffect = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = withoutEffect } });
                    int changed = DifferentRgbPixels(visible, scene.Render(plain, 320, 224));
                    if (changed > peakChangedPixels)
                    {
                        peakChangedPixels = changed;
                        NativeBattleRestorationPresentationTests.Publish(output, $"visible-peak-{current.NativeGraphics.Callback}", current, visible);
                    }
                }
                if (after?.ReturnedDescriptor is > 0 && frame.LastCommands.Any(IsStone))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame withoutStone = frame with { LastCommands = frame.LastCommands.Where(command => !IsStone(command)).ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = withoutStone } });
                    int changed = DifferentRgbPixels(visible, scene.Render(plain, 320, 224));
                    if (changed > peakStoneChangedPixels)
                    {
                        peakStoneChangedPixels = changed;
                        NativeBattleRestorationPresentationTests.Publish(output, $"visible-stone-peak-{current.NativeGraphics.Callback}", current, visible);
                    }
                }
                bool IsStone(NativeBattleLogicalResolvedCommand command) => command.Command.Opcode is 0x24 or 0x2C && command.Command.Words.Length >= 5 && command.Command.Words[2] >> 16 == 0x3D54 && command.Command.Words[4] >> 16 == 0xB8;
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104) && current.ActiveAction is null && current.QueuedActions.Length == 0)
                {
                    break;
                }
            }
            Assert.True(ticks < item.MaximumTicks, "The explicit diagnostic tick budget was exceeded; it is not an effect duration.");
            Assert.True(initialized);
            Assert.Empty(pending);
            Assert.Equal(1, completions);
            Assert.Equal(0, statusTransitions);
            Assert.Equal(1, contactAgeCrossings);
            Assert.Equal(1, graphicsStatusTransitions);
            Assert.Equal(0, contactSequenceTransitions);
            Assert.True(observedPrivatePackets);
            Assert.True(peakStoneChangedPixels > 0, "The dithered posed stone actor must contribute visible RGB pixels.");
            NativeBattleModuleSound heard = Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == installed.Break117Resources!.Sound).NativeModuleSound!;
            Assert.Equal(installed.Break117Resources!.Sound, heard.Sound);
            Assert.True(peakChangedPixels > 0, "Physical shell and particle packets must contribute visible RGB pixels.");
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
            ColdNext("completed", battle.Capture(), scene);
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
            Report("checked", null);

            void ColdNext(string name, BattleSnapshot snapshot, BattleScenePresentation presentation, bool advanceOriginal = true)
            {
                BattleSession running = advanceOriginal ? battle ?? throw new InvalidDataException("The ordinary cast lost its session.") : BattleSession.Restore(snapshot);
                NativeBattleRestorationPresentationTests.Publish(output, name + "-before", snapshot, presentation.Render(running, 320, 224));
                BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                Assert.Equal(presentation.Render(running, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                running.Tick();
                cold.Tick();
                Assert.Equal(JsonSerializer.Serialize(running.Capture()), JsonSerializer.Serialize(cold.Capture()));
                byte[] pixels = presentation.Render(running, 320, 224).ToArray();
                Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                NativeBattleRestorationPresentationTests.Publish(output, name + "-after", running.Capture(), pixels);
                restoredBoundaries.Add(name);
            }
        }
        catch (Exception error)
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            }
            Report("blocked", error.ToString());
            throw;
        }

        void Report(string status, string? blocker)
        {
            object result = new { Status = status, FirstBlocker = blocker, item.Id, item.Effect, action.Command, action.ActionId, KernelSection = action.KernelSection, KernelIndex = action.KernelIndex, Entry = installed.Entry, InputScenario = inputScenario, SourceSnapshotSha256 = sourceSha256, Ticks = ticks, NativeCallbackDelta = battle?.NativeGraphics?.Callback - sourceCallback, DiagnosticMaximumTicks = item.MaximumTicks, ProcessTimeoutSeconds = item.ProcessTimeoutSeconds, ProcessTimeoutOwner = "External launcher must enforce this case's process deadline", Completions = completions, ContactObserver = contactObserver, StatusPresentationTransitions = statusTransitions, Target78ContactCrossings = contactAgeCrossings, GraphicsStatusTransitions = graphicsStatusTransitions, PeakChangedPixels = peakChangedPixels, PeakStoneChangedPixels = peakStoneChangedPixels, ContactSequenceObservableTransitions = contactSequenceTransitions, RestoredBoundaries = restoredBoundaries, ObservedPrivatePackets = observedPrivatePackets, ObservedAdditivePackets = observedAdditivePackets, InstalledSavedStatusMask = action.Statuses0, InstalledBattleStatusMask = action.Statuses1, OriginalSound = installed.Break117Resources?.Sound, GraphicsScope = "Physical module packet RGB contribution and exact RGBA next-Tick equality at produced, live and completed boundaries", FormulaProducer = exported?.Scenario, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), ManifestSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest!))), Pending = new[] { "Story-route continuation", "Whole-entry native differential comparison", "Other spell targets and stocks" } };
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }

        NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { } effect && effect.Binding.Effect == item.Effect && effect.Binding.Entry == installed.Entry ? effect : null;
    }

    private static int TargetAge(NativeBattleOriginalEffectState? effect)
    {
        NativeBattleModulePoolTask? target = effect?.Pools.SelectMany(pool => pool.Slots).SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x6C1950);
        return target is null ? -1 : unchecked((short)target.ReadWord(12));
    }

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

    private static int Stock(BattleSnapshot snapshot, string actor, ushort action) => (snapshot.Combatants.Single(item => item.Id == actor).NativeMagicInventory ?? throw new InvalidDataException("The ordinary fixture lacks its canonical native magic inventory.")).Single(stock => stock.SpellId == action).Count;
}
