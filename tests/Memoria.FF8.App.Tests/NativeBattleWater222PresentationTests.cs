using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWater222PresentationTests
{
    private const string SourceSha256 = "02BC7D279972F7B5814D5DDE1EEEFA43DD1E482C40C6557BAD2263B2EB4E7822";
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Controlled_Water_runs_original_scripts_IO_draws_contact_and_independent_live_restores_to_completion()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Select the authenticated before-queued controlled component explicitly.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_OUTPUT") ?? throw new InvalidDataException("A distinct Water evidence output is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_MAX_CALLBACKS") is { } text ? int.Parse(text) : 4096;
        Assert.True(maximum > 0);
        byte[] sourceBytes = File.ReadAllBytes(sourcePath!);
        Assert.Equal(SourceSha256, Convert.ToHexString(SHA256.HashData(sourceBytes)));
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        int descriptor = Assert.Single(original.NativeGraphics!.Descriptors.Queue, index => original.NativeGraphics.Descriptors.Packets[index].Command == 104);
        uint offset = unchecked(original.NativeGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
        Assert.True(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        int headerIndex = checked((int)(offset / 20));
        NativeBattleActionPacketsState actionPackets = Assert.IsType<NativeBattleActionPacketsState>(original.NativeActionPackets);
        NativeBattleActionHeaderSlot header = actionPackets.Headers[headerIndex];
        Assert.Equal((byte)240, header.ReadByte(1));
        Assert.Equal((ushort)5, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)100, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = actionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((ushort)100, original.NativePartyPresentation!.Rows[0]!.HitPoints);
        NativeBattleEffectMemory inherited = Assert.IsType<NativeBattleEffectMemory>(original.NativeEffectMemory);
        Assert.Null(inherited.ScriptedEffects);
        Assert.Null(inherited.ScriptedGraphics);
        Assert.Null(inherited.Water222);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([222])[221];
        NativeBattleWater222Resources resources = Assert.IsType<NativeBattleWater222Resources>(binding.Water222Resources);
        resources.Validate();
        NativeBattleWaterIoResources ioResources = Assert.IsType<NativeBattleWaterIoResources>(resources.Io);
        ioResources.Validate();
        NativeBattleActionHeaderSlot replaced = header with { Payload = header.Payload.SetItem(6, 222).SetItem(7, 0) };
        BattleSnapshot source = original with
        {
            Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(221, binding) },
            NativeActionPackets = actionPackets with { Headers = actionPackets.Headers.SetItem(headerIndex, replaced) },
            NativeEffectMemory = inherited with { ScriptedEffects = NativeBattleScriptedEffectMemory.FromColdProcess(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() }
        };
        NativeBattleRestorationPresentationTests.Publish(output, "controlled-input", source, null);
        File.WriteAllText(Path.Combine(output, "fixture-selection.json"), JsonSerializer.Serialize(new
        {
            Source = Path.GetFullPath(sourcePath!),
            SourceSha256,
            Scenario = "Controlled command240/action5/header effect100 changed to222 with seven-HP contact; original Water command and formula admission remain separate.",
            ChangedFields = new[] { $"NativeActionPackets.Headers[{headerIndex}].Payload[6..7]: 100 to222", "Definition.NativeEffectCatalog[221]: installed authenticated Water binding/resources", "NativeEffectMemory.ScriptedEffects: explicitly declared fresh process BSS owner", "NativeEffectMemory.ScriptedGraphics: explicitly declared fresh process owner followed by InitGeom56BAA0" },
            InheritedBodyMatrices = "Supplied component's explicitly migrated current-body-matrix owners retained unchanged; no recomputation or missing-owner hydration",
            InitializedZsf3 = source.NativeEffectMemory!.ScriptedGraphics!.Zsf3,
            OrdinaryWaterAdmission = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        HashSet<string> required = ["initialized", "expanded-slots", "mesh-morph", "rectangles", "particles", "scroll", "actor-override", "io-pending"];
        HashSet<byte> drawKinds = [];
        List<Boundary> boundaries = [];
        List<short> files = [];
        int callbacks = 0;
        int contacts = 0;
        int completions = 0;
        int ioCompletions = 0;
        int packetCallbacks = 0;
        int peakSlots = 0;
        bool initialized = false;
        bool changedArena = false;
        BattleSnapshot prior = source;
        try
        {
            NativeBattleRestorationPresentationTests.Publish(output, "initial", source, scene.Render(battle, 320, 224));
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The diagnostic callback bound is not a native Water duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Effect(prior);
                string? name = BoundaryName(prior, required);
                BattleSession? cold = null;
                if (name is not null)
                {
                    NativeBattleRestorationPresentationTests.Publish(output, name + "-before", prior, scene.Render(battle, 320, 224));
                    cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
                    Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(cold.Capture()));
                }
                battle.AdvanceNativeGraphicsPresentation(Callback);
                callbacks++;
                audio.Update(battle);
                BattleSnapshot current = battle.Capture();
                Assert.Equal(prior.NativeGraphics!.Callback + 1, current.NativeGraphics!.Callback);
                NativeBattleOriginalEffectState? after = Effect(current);
                initialized |= after?.ReturnedDescriptor is > 0;
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                ushort hpBefore = prior.NativePartyPresentation!.Rows[0]!.HitPoints;
                ushort hpAfter = current.NativePartyPresentation!.Rows[0]!.HitPoints;
                if (hpBefore != hpAfter)
                {
                    Assert.Equal((ushort)100, hpBefore);
                    Assert.Equal((ushort)93, hpAfter);
                    contacts++;
                }
                Assert.True(hpAfter is 100 or 93);
                if (current.NativeEffectMemory?.Water222 is not null)
                {
                    NativeBattleScriptedEffectMemory core = current.NativeEffectMemory.ScriptedEffects!;
                    peakSlots = Math.Max(peakSlots, Word(core, 0x27970A0));
                    foreach ((byte kind, int slot) in DrawSlots(current))
                    {
                        drawKinds.Add(kind);
                    }
                    byte busy = core.Read(0x2798219, 1)[0];
                    byte previousBusy = prior.NativeEffectMemory?.Water222 is null ? (byte)0 : prior.NativeEffectMemory.ScriptedEffects!.Read(0x2798219, 1)[0];
                    if (busy != 0 && previousBusy == 0)
                    {
                        uint manager = Dword(core, 0x27973EC);
                        uint destination = Dword(core, manager + 0xB4);
                        byte[] copied = current.NativeEffectMemory.Arena!.Read(ArenaOffset(destination), 0x8000);
                        NativeBattleArchiveFile file = Assert.Single(ioResources.Files, item => item.Bytes.AsSpan().SequenceEqual(copied));
                        files.Add(checked((short)file.FileId));
                        Assert.Contains(current.NativeFileIo!.Rows, row => row.SchedulerCallback != 0 && row.CompletionCallback == 0xAA0ED0 && row.FileResult == 0x8000);
                    }
                    if (previousBusy != 0 && busy == 0)
                    {
                        ioCompletions++;
                    }
                    changedArena |= current.NativeEffectMemory.Cursor > resources.Files.Sum(file => file.Data.Length);
                }
                packetCallbacks += current.NativeGraphics.PrimitiveFrame?.LastCommands.Any(item => (item.Command.Opcode & 0xFC) is 0x24 or 0x2C or 0x34 or 0x3C) == true ? 1 : 0;
                scene.Render(battle, 320, 224);
                if (cold is not null)
                {
                    cold.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold.Capture()));
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output, name + "-after", current, pixels);
                    boundaries.Add(new(name!, prior.NativeGraphics.Callback, current.NativeGraphics.Callback, Convert.ToHexString(SHA256.HashData(pixels))));
                    required.Remove(name!);
                }
            }
            Assert.True(initialized);
            Assert.Equal(1, completions);
            Assert.Equal(1, contacts);
            Assert.Equal((ushort)93, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
            Assert.Equal(new short[] { 363, 364, 365 }, files.Distinct().Order().ToArray());
            Assert.Equal(files.Count, ioCompletions);
            Assert.True(packetCallbacks > 0);
            Assert.True(peakSlots >= 96);
            Assert.All(new byte[] { 2, 5, 6, 0x15, 0x26 }, kind => Assert.Contains(kind, drawKinds));
            Assert.Empty(required);
            Assert.NotEmpty(heard);
            NativeBattleOriginalEffectState finished = Assert.IsType<NativeBattleOriginalEffectState>(battle.NativeGraphics!.OriginalEffect);
            Assert.Equal((ushort)222, finished.Binding.Effect);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.Equal((byte)0, battle.NativeEffectMemory.ScriptedEffects!.Read(0x2798219, 1)[0]);
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
            BattleSession completedCold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            completedCold.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(completedCold.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(completedCold, 320, 224).ToArray());
            Assert.False(HasAction(battle));
            Assert.Equal(SourceSha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))));
            Report("checked", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failure-current", battle.Capture(), null);
            Report("failed", error.ToString());
            throw;
        }

        void Report(string status, string? blocker)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
            {
                Status = status,
                FirstBlocker = blocker,
                Effect = 222,
                Scenario = "Explicit controlled effect substitution with fresh scripted process/initialized graphics ingress; original ordinary Water admission/formula separate",
                Callbacks = callbacks,
                Completions = completions,
                ContactHpTransitions = contacts,
                Files = files,
                FileCompletions = ioCompletions,
                DrawKinds = drawKinds.Order(),
                PeakSlotCapacity = peakSlots,
                PacketCallbacks = packetCallbacks,
                ObservedExtendedArenaCursor = changedArena,
                Heard = heard,
                Boundaries = boundaries,
                ObservedInitialCrtSeed = source.NativeCrtRandom?.Seed,
                ObservedFinalCrtSeed = battle.Capture().NativeCrtRandom?.Seed,
                SourceSha256,
                BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(),
                Pending = new[] { "Whole-entry native visual comparison", "Ordinary command and original formula admission", "Other headers/targets", "Hardware audibility" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested)
    {
        if (snapshot.NativeEffectMemory?.Water222 is null || Effect(snapshot)?.ReturnedDescriptor is not > 0)
        {
            return null;
        }
        NativeBattleScriptedEffectMemory core = snapshot.NativeEffectMemory.ScriptedEffects!;
        (byte Kind, int Slot)[] slots = DrawSlots(snapshot).ToArray();
        NativeBattleEffectArena arena = snapshot.NativeEffectMemory.Arena!;
        (string Name, bool Ready)[] candidates =
        [
            ("initialized", true),
            ("expanded-slots", Word(core, 0x27970A0) >= 96),
            ("mesh-morph", slots.Any(item => item.Kind == 2 && BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(item.Slot + 0xBC, 4)) != 0)),
            ("rectangles", slots.Any(item => item.Kind == 5)),
            ("particles", slots.Any(item => item.Kind == 6)),
            ("scroll", slots.Any(item => item.Kind == 0x15)),
            ("actor-override", slots.Any(item => item.Kind == 0x26)),
            ("io-pending", core.Read(0x2798219, 1)[0] != 0),
            ("contact-applied", snapshot.NativePartyPresentation!.Rows[0]!.HitPoints == 93)
        ];
        return candidates.Where(item => item.Ready && requested.Contains(item.Name)).Select(item => item.Name).FirstOrDefault();
    }

    private static IEnumerable<(byte Kind, int Slot)> DrawSlots(BattleSnapshot snapshot)
    {
        NativeBattleScriptedEffectMemory core = snapshot.NativeEffectMemory!.ScriptedEffects!;
        NativeBattleEffectArena arena = snapshot.NativeEffectMemory.Arena!;
        uint manager = Dword(core, 0x27973EC);
        int slotBase = ArenaOffset(Dword(core, manager + 0x90));
        for (int i = 0; i < 128; i++)
        {
            byte entry = core.Read(0x2797554 + (uint)i, 1)[0];
            if (entry == 0xFF)
            {
                yield break;
            }
            int slot = checked(slotBase + 0x100 * (entry & 0x7F));
            yield return (arena.Read(slot + 0x1C, 1)[0], slot);
        }
        throw new InvalidDataException("Water evidence requires its bounded terminated draw-order owner.");
    }
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(index => battle.NativeGraphics.Descriptors.Packets[index].Command == 104);
    private static NativeBattleOriginalEffectState? Effect(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 222 } effect ? effect : null;
    private static uint Dword(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
    private static ushort Word(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(core.Read(address, 2));
    private static int ArenaOffset(uint pointer) => checked((int)(pointer - 0x20DFAB8u));
    private sealed record Boundary(string Name, long Before, long After, string RgbaSha256);
}
