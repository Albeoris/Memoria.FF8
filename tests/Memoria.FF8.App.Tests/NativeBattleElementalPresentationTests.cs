using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleElementalPresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_elemental_party_contact_has_visible_packets_and_independent_live_boundary_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_ELEMENTAL_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_ELEMENTAL_SNAPSHOT to a complete queued or early initialized standalone elemental party-contact fixture.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ELEMENTAL_OUTPUT") ?? throw new InvalidDataException("A distinct MEMORIA_FF8_ELEMENTAL_OUTPUT directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(), "Evidence output must be a distinct empty directory.");
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        NativeBattleGraphicsState initialGraphics = source.NativeGraphics ?? throw new InvalidDataException("The fixture lacks its native graphics owner.");
        NativeBattleActionPacketsState packets = source.NativeActionPackets ?? throw new InvalidDataException("The fixture lacks its physical action packets.");
        int descriptorSlot = Assert.Single(initialGraphics.Descriptors.Queue, slot => initialGraphics.Descriptors.Packets[slot].Command == 104);
        uint offset = unchecked(initialGraphics.Descriptors.Packets[descriptorSlot].Argument - 0x1D280C4u);
        Assert.True(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        int headerIndex = (int)(offset / 20);
        NativeBattleActionHeaderSlot header = packets.Headers[headerIndex];
        ushort effectId = NativeBattleActionGraphics.Word(header, 6);
        EffectCase spec = effectId switch
        {
            142 => new(142, 0x61CE10, 0x24BFB68, 0x24BFB78, 0x61DD90, 0x61CEC0, 45, 300003, 0x100, [9, 11, 14, 20]),
            143 => new(143, 0x61BFE0, 0x24BECE0, 0x24BECF0, 0x61CDA0, 0x61C0A0, 70, 300056, 0, [4, 14, 33, 48, 60]),
            102 => new(102, 0x6DC2D0, 0x2544DD0, 0x2544DE0, 0x6DC390, 0x6DC510, 38, 300009, 0x101, [4, 12, 24, 26, 31], [0x2544DF0, 0x2544E00], false),
            103 => new(103, 0x6DA300, 0x2543EB8, 0x2543EC8, 0x6DA3A0, 0x6DAA30, 72, 300015, 0x101, [20, 30, 45, 50, 60, 69], [0x2543ED8, 0x2543EE8, 0x2543EF8, 0x2543F08], false),
            104 => new(104, 0x6D6310, 0x2543E20, 0x2543E30, 0x6D6400, 0x6D67E0, 100, 300089, 0x101, [8, 28, 33, 51, 68, 82, 97], [0x2543E40, 0x2543E50, 0x2543E60, 0x2543E70, 0x2543E80]),
            118 => new(118, 0x6C0C80, 0x2521640, 0x2521630, 0x6C0D20, 0x6C0EC0, 65, 300110, 1, [11, 12, 20, 38, 47, 48], [0x2521620], false),
            _ => throw new NotSupportedException("The fixture needs a proved elemental presentation recipe.")
        };
        Assert.True(initialGraphics.ActionGraphics is null || initialGraphics.ActionGraphics.HeaderIndex == headerIndex);
        Assert.Equal((byte)0, header.ReadByte(17));
        NativeBattleContactPacket contact = packets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("An explicit contact pointer is required."));
        int target = contact.Primary.ActorIndex;
        Assert.InRange(target, 0, 2);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        ushort initialHp = source.NativePartyPresentation!.Rows[target]!.HitPoints;
        Assert.True(initialHp >= 7);
        ushort expectedHp = (ushort)(initialHp - 7);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[effectId - 1]));
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        Assert.DoesNotContain(battle.Events, item => item.NativeModuleSound?.Sound == spec.Sound);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        List<Boundary> boundaries = [];
        List<short> stageDepths = [];
        HashSet<int> requested = spec.Phases.ToHashSet();
        int callbacks = 0;
        int preEntryCallbacks = 0;
        int localCallbacks = 0;
        int asynchronousCallbacks = 0;
        int pausedCallbacks = 0;
        int moduleWaitCallbacks = 0;
        int contactTransitions = 0;
        bool initialized = false;
        bool stageChanged = false;
        BattleSnapshot prior = source;
        NativeBattleRestorationPresentationTests.Publish(output, "initial", source, scene.Render(battle, 320, 224));
        try
        {
            while (HasAction(battle))
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? beforeEffect = Selected(prior, spec);
                NativeBattleModulePoolTask? controller = Controller(beforeEffect, spec);
                bool paused = (prior.NativeGraphics!.Flags & 0x201) != 0;
                int? phase = controller is null ? null : unchecked((short)controller.ReadWord(12));
                bool boundary = !paused && phase is int value && requested.Contains(value);
                BattleSession? independent = null;
                if (boundary)
                {
                    string name = "phase-" + phase!.Value;
                    NativeBattleRestorationPresentationTests.Publish(output, name + "-before", prior, scene.Render(battle, 320, 224));
                    independent = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
                    Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(independent, 320, 224).ToArray());
                }
                ushort? rootCounter = RootCounter(beforeEffect, spec);
                long nativeBefore = prior.NativeGraphics.Callback;
                battle.AdvanceNativeGraphicsPresentation(Callback);
                callbacks++;
                BattleSnapshot current = battle.Capture();
                Assert.Equal(nativeBefore + 1, current.NativeGraphics!.Callback);
                audio.Update(battle);
                NativeBattleOriginalEffectState? afterEffect = Selected(current, spec);
                initialized |= afterEffect?.ReturnedDescriptor is > 0;
                if (!initialized)
                {
                    Assert.True(++preEntryCallbacks <= 512, "The explicit pre-entry diagnostic callback budget was exceeded; this is not a native effect duration.");
                }
                else if (beforeEffect is not null && ChildrenLive(beforeEffect, spec))
                {
                    if (paused)
                    {
                        Assert.True(++pausedCallbacks <= 512, "The explicit paused-state diagnostic budget was exceeded.");
                    }
                    else if (RootCounter(afterEffect, spec) != rootCounter)
                    {
                        Assert.True(++localCallbacks <= spec.MaximumLocalCallbacks, "Single-header elemental children exceed their proved unpaused module-callback envelope.");
                    }
                    else
                    {
                        Assert.True(++moduleWaitCallbacks <= 512, "The explicit module-dispatch wait diagnostic budget was exceeded.");
                    }
                }
                else
                {
                    Assert.True(++asynchronousCallbacks <= 512, "The explicit asynchronous completion/descriptor diagnostic budget was exceeded.");
                }
                ushort priorHp = prior.NativePartyPresentation!.Rows[target]!.HitPoints;
                ushort hp = current.NativePartyPresentation!.Rows[target]!.HitPoints;
                if (hp != priorHp)
                {
                    Assert.Equal(initialHp, priorHp);
                    Assert.Equal(expectedHp, hp);
                    contactTransitions++;
                }
                Assert.True(hp == initialHp || hp == expectedHp);
                foreach (var group in battle.NativeStage!.Groups)
                {
                    stageChanged |= group.ColorDepth != 0;
                    if (stageDepths.Count == 0 || stageDepths[^1] != group.ColorDepth)
                    {
                        stageDepths.Add(group.ColorDepth);
                    }
                }
                if (independent is not null)
                {
                    independent.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(independent.Capture()));
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(independent, 320, 224).ToArray());
                    Assert.NotEqual(phase, Controller(afterEffect, spec) is { } afterController ? unchecked((short)afterController.ReadWord(12)) : null);
                    NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                    int packetCount = frame.LastCommands.Count(command => command.Command.Packet.Bank == "20DFAB8");
                    NativeBattlePrimitiveFrame without = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    byte[] background = scene.Render(plain, 320, 224).ToArray();
                    int changedRgb = Enumerable.Range(0, pixels.Length / 4).Count(pixel => !pixels.AsSpan(pixel * 4, 3).SequenceEqual(background.AsSpan(pixel * 4, 3)));
                    NativeBattleRestorationPresentationTests.Publish(output, "phase-" + phase!.Value + "-after", current, pixels);
                    boundaries.Add(new(phase.Value, prior.Frame, current.Frame, current.NativeGraphics.Callback, packetCount, changedRgb, Convert.ToHexString(SHA256.HashData(pixels))));
                    requested.Remove(phase.Value);
                }
            }
            Assert.True(initialized);
            Assert.Empty(requested);
            Assert.Equal(spec.Phases.Length, boundaries.Count);
            Assert.Contains(boundaries, item => item.EffectPackets > 0 && item.ChangedRgbPixels > 0);
            Assert.Equal(1, contactTransitions);
            Assert.Equal(expectedHp, battle.Capture().NativePartyPresentation!.Rows[target]!.HitPoints);
            Assert.Equal(spec.ChangesStage, stageChanged);
            Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            NativeBattleModuleSound sound = Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == spec.Sound).NativeModuleSound!;
            Assert.Equal(spec.SoundFlags, sound.Flags);
            Assert.Single(heard, item => item.Sound == spec.Sound);
            NativeBattleOriginalEffectState finished = Assert.IsType<NativeBattleOriginalEffectState>(battle.NativeGraphics!.OriginalEffect);
            Assert.Equal(spec.Effect, finished.Binding.Effect);
            Assert.Equal(spec.Entry, finished.Binding.Entry);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            BattleSnapshot completed = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, "completed", completed, scene.Render(battle, 320, 224));
            BattleSession restoredCompleted = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            restoredCompleted.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restoredCompleted.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(restoredCompleted, 320, 224).ToArray());
            Assert.False(HasAction(battle));
            Assert.Equal(expectedHp, battle.Capture().NativePartyPresentation!.Rows[target]!.HitPoints);
            audio.Update(restoredCompleted);
            Assert.Single(heard, item => item.Sound == spec.Sound);
            Report("checked", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            Report("failed", error.ToString());
            throw;
        }

        void Report(string status, string? blocker)
        {
            object report = new { Status = status, FirstBlocker = blocker, Effect = effectId, SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), Callbacks = callbacks, PreEntryDiagnosticCallbacks = preEntryCallbacks, UnpausedLocalModuleCallbacks = localCallbacks, ModuleWaitDiagnosticCallbacks = moduleWaitCallbacks, AsyncDiagnosticCallbacks = asynchronousCallbacks, PausedDiagnosticCallbacks = pausedCallbacks, PartyTarget = target, InitialDisplayedHp = initialHp, ExpectedDisplayedHp = expectedHp, ContactObservableTransitions = contactTransitions, Boundaries = boundaries, StageDepths = stageDepths, RoutedSounds = heard.Count(item => item.Sound == spec.Sound), BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Native differential comparison", "Hardware audibility", "Other targets/headers/variants", "Formula and ordinary admission" } };
            using FileStream stream = new(Path.Combine(output, "result.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(stream, report, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot, EffectCase spec) => snapshot.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: not null } effect && effect.Binding.Effect == spec.Effect && effect.Binding.Entry == spec.Entry ? effect : null;
    private static NativeBattleModulePoolTask? Controller(NativeBattleOriginalEffectState? effect, EffectCase spec) => effect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == spec.ChildDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == spec.ControllerCallback && task.ReadWord(14) == 0);
    private static bool ChildrenLive(NativeBattleOriginalEffectState effect, EffectCase spec) => effect.Pools.Any(pool => (pool.DescriptorAddress == spec.ChildDescriptor || spec.AdditionalChildDescriptors?.Contains(pool.DescriptorAddress) == true) && pool.Head is not null);
    private static ushort? RootCounter(NativeBattleOriginalEffectState? effect, EffectCase spec) => effect?.Pools.Single(pool => pool.DescriptorAddress == spec.MainDescriptor).Slots.Single(task => task.Callback == spec.RootCallback).ReadWord(12);
    private sealed record EffectCase(ushort Effect, uint Entry, uint MainDescriptor, uint ChildDescriptor, uint RootCallback, uint ControllerCallback, int MaximumLocalCallbacks, uint Sound, uint SoundFlags, int[] Phases, uint[]? AdditionalChildDescriptors = null, bool ChangesStage = true);
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private sealed record Boundary(int ControllerPhase, long BeforeFrame, long AfterFrame, long NativeCallback, int EffectPackets, int ChangedRgbPixels, string RgbaSha256);
}
