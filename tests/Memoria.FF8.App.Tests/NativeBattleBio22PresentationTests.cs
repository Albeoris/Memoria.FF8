using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBio22PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Controlled_original_Bio_completes_party_contact_with_retained_owners_and_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_BIO22_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Select a complete fresh before-queued Bio party-contact component explicitly.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BIO22_OUTPUT") ?? throw new InvalidDataException("A distinct Bio output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_BIO22_MAX_CALLBACKS") is { } text ? int.Parse(text) : 2048;
        Assert.True(maximum > 0);
        byte[] sourceBytes = File.ReadAllBytes(sourcePath!);
        string sourceSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes));
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
        NativeBattleActionPacketsState actionPackets = Assert.IsType<NativeBattleActionPacketsState>(source.NativeActionPackets);
        int descriptor = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
        uint headerOffset = unchecked(graphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
        Assert.True(headerOffset % 20 == 0 && headerOffset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        int headerIndex = checked((int)(headerOffset / 20));
        NativeBattleActionHeaderSlot header = actionPackets.Headers[headerIndex];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal((ushort)12, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)22, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = actionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((ushort)100, source.NativePartyPresentation!.Rows[0]!.HitPoints);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([22])[21];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[21]));
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        resources.Validate();
        NativeBattleEffectMemory memory = Assert.IsType<NativeBattleEffectMemory>(source.NativeEffectMemory);
        Assert.Equal(resources.MeshBaseline.Bytes.ToArray(), Assert.IsType<NativeBattleBio22Geometry>(memory.Bio22Geometry).Bytes.ToArray());
        Assert.Equal(resources.SecondMeshBaseline.Bytes.ToArray(), Assert.IsType<NativeBattleBio22Geometry>(memory.Bio22SecondaryGeometry).Bytes.ToArray());
        Assert.Equal(resources.ThirdMeshBaseline.Bytes.ToArray(), Assert.IsType<NativeBattleBio22Geometry>(memory.Bio22ThirdGeometry).Bytes.ToArray());
        Assert.Equal(resources.FourthMeshBaseline.Bytes.ToArray(), Assert.IsType<NativeBattleBio22Geometry>(memory.Bio22FourthGeometry).Bytes.ToArray());
        Assert.Equal(resources.PaletteBaseline.Data.ToArray(), Assert.IsType<NativeBattleBio22Palette>(memory.Bio22Palette).Bytes.ToArray());
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        List<int> impacts = [];
        List<NativeBattleMusicCommandReceipt> music = [];
        int stops = 0;
        BattleAudioPresentation audio = new(impacts.Add, playModule: heard.Add, stopSounds: () =>
        {
            stops++;
        }, playMusicCommand: music.Add);
        audio.Update(battle);
        heard.Clear();
        impacts.Clear();
        music.Clear();
        stops = 0;
        int initialEvents = battle.Events.Count;
        HashSet<string> required = ["initialized", "first-mesh-morph", "first-deformed", "stage-peak", "third-mesh-morph", "fourth-mesh-morph", "palette-live", "particle-live", "later-particle-live", "overlay-live", "final-actor-live", "stage-falling"];
        List<Boundary> boundaries = [];
        HashSet<uint> callbacksSeen = [];
        HashSet<uint> particleCallbacks = [];
        HashSet<uint> soundIds = [];
        List<short> stageDepths = [];
        int callbacks = 0;
        int contacts = 0;
        int completions = 0;
        int packetCallbacks = 0;
        int particlePeak = 0;
        int geometryPeak = 0;
        bool initialized = false;
        bool commonIo = false;
        bool firstChanged = false;
        bool secondChanged = false;
        bool thirdChanged = false;
        bool fourthChanged = false;
        bool paletteChanged = false;
        BattleSnapshot prior = source;
        try
        {
            NativeBattleRestorationPresentationTests.Publish(output, "initial", source, scene.Render(battle, 320, 224));
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The diagnostic callback budget is not a native Bio duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                string? name = BoundaryName(prior, required, resources);
                BattleSession? independent = null;
                if (name is not null)
                {
                    NativeBattleRestorationPresentationTests.Publish(output, name + "-before", prior, scene.Render(battle, 320, 224));
                    independent = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
                    Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(independent, 320, 224).ToArray());
                }
                battle.AdvanceNativeGraphicsPresentation(Callback);
                callbacks++;
                audio.Update(battle);
                BattleSnapshot current = battle.Capture();
                Assert.Equal(prior.NativeGraphics!.Callback + 1, current.NativeGraphics!.Callback);
                NativeBattleOriginalEffectState? after = Selected(current);
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
                NativeBattleModulePoolTask[] tasks = Tasks(after);
                callbacksSeen.UnionWith(tasks.Select(task => task.Callback));
                particleCallbacks.UnionWith(tasks.Where(IsParticle).Select(task => task.Callback));
                particlePeak = Math.Max(particlePeak, tasks.Count(IsParticle));
                geometryPeak = Math.Max(geometryPeak, tasks.Count(task => task.Callback is 0x89AAE0 or 0x89C270));
                NativeBattleEffectMemory currentMemory = current.NativeEffectMemory!;
                firstChanged |= Changed(currentMemory.Bio22Geometry, resources.MeshBaseline.Bytes);
                secondChanged |= Changed(currentMemory.Bio22SecondaryGeometry, resources.SecondMeshBaseline.Bytes);
                thirdChanged |= Changed(currentMemory.Bio22ThirdGeometry, resources.ThirdMeshBaseline.Bytes);
                fourthChanged |= Changed(currentMemory.Bio22FourthGeometry, resources.FourthMeshBaseline.Bytes);
                paletteChanged |= currentMemory.Bio22Palette is { } palette && !palette.Bytes.SequenceEqual(resources.PaletteBaseline.Data);
                if (current.NativeStage is { } stage)
                {
                    stageDepths.Add(stage.Groups[0].ColorDepth);
                }
                commonIo |= current.NativeFileIo?.Rows.Any(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470) == true;
                packetCallbacks += current.NativeGraphics.PrimitiveFrame?.LastCommands.Any(item => (item.Command.Opcode & 0xFC) is 0x24 or 0x2C or 0x34 or 0x3C) == true ? 1 : 0;
                if (independent is not null)
                {
                    independent.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(independent.Capture()));
                    byte[] rgba = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(rgba, new BattleScenePresentation(assets, source.Definition, true).Render(independent, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output, name + "-after", current, rgba);
                    boundaries.Add(new(name!, prior.NativeGraphics.Callback, current.NativeGraphics.Callback, Convert.ToHexString(SHA256.HashData(rgba))));
                    required.Remove(name!);
                }
            }
            Assert.True(initialized);
            Assert.Equal(1, completions);
            Assert.Equal(1, contacts);
            Assert.Equal((ushort)93, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
            Assert.True(firstChanged && secondChanged && thirdChanged && fourthChanged && paletteChanged);
            Assert.True(geometryPeak > 0 && particlePeak > 0 && packetCallbacks > 0);
            Assert.Contains((short)1024, stageDepths);
            Assert.Empty(required);
            NativeBattleOriginalEffectState finished = Assert.IsType<NativeBattleOriginalEffectState>(Selected(battle.Capture()));
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            NativeBattleModuleSound[] receipts = battle.Events.Skip(initialEvents).Where(item => item.NativeModuleSound is not null).Select(item => item.NativeModuleSound!).ToArray();
            Assert.NotEmpty(receipts);
            Assert.Contains(receipts, sound => sound.Sound == resources.Sound);
            Assert.Equal(receipts.Select(sound => sound.Sound), heard.Select(sound => sound.Sound));
            soundIds.UnionWith(receipts.Select(sound => sound.Sound));
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
            BattleSession completedCold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            completedCold.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(completedCold.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(completedCold, 320, 224).ToArray());
            Assert.False(HasAction(battle));
            Assert.Equal(sourceSha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))));
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
                Effect = 22,
                Command = 2,
                Action = 12,
                Scenario = "Fresh standalone original-resource controlled party contact, seven HP already supplied after formula; no missing-owner hydration or fixture substitution",
                Source = Path.GetFullPath(sourcePath!),
                SourceSha256 = sourceSha256,
                BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(),
                Callbacks = callbacks,
                Completions = completions,
                ContactHpTransitions = contacts,
                InitialHp = 100,
                FinalHp = battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints,
                AllFourMeshesChanged = firstChanged && secondChanged && thirdChanged && fourthChanged,
                OwnerChanges = new { First = firstChanged, Second = secondChanged, Third = thirdChanged, Fourth = fourthChanged, Palette = paletteChanged },
                GeometryPeak = geometryPeak,
                ParticlePeak = particlePeak,
                ParticleCallbacks = particleCallbacks.Order(),
                CallbacksSeen = callbacksSeen.Order(),
                PacketCallbacks = packetCallbacks,
                PacketObservation = "Parsed textured commands include ordinary scene packets; dedicated leaf tests establish effect packet ownership",
                StageDepths = stageDepths.Distinct().Order(),
                CommonIoObserved = commonIo,
                Heard = heard,
                SoundIds = soundIds,
                ImpactRequests = impacts,
                StopSoundRequests = stops,
                MusicCommands = music,
                Boundaries = boundaries,
                Pending = new[] { "Whole-entry native visual comparison", "Ordinary command producer and original formula admission", "Other headers and targets", "Hardware audibility" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> required, NativeBattleBio22Resources resources)
    {
        NativeBattleOriginalEffectState? effect = Selected(snapshot);
        if (effect?.ReturnedDescriptor is not > 0 || snapshot.NativeEffectMemory is not { } memory)
        {
            return null;
        }
        NativeBattleModulePoolTask[] tasks = Tasks(effect);
        (string Name, bool Ready)[] candidates =
        [
            ("initialized", true),
            ("third-mesh-morph", tasks.Any(task => task.Callback == 0x89C270 && task.ReadDword(0x4C) == 0x1619888) && Changed(memory.Bio22ThirdGeometry, resources.ThirdMeshBaseline.Bytes)),
            ("overlay-live", tasks.Any(task => task.Callback == 0x89C650 && task.ReadWord(0x24) > 0)),
            ("first-mesh-morph", tasks.Any(task => task.Callback == 0x89AAE0) && Changed(memory.Bio22Geometry, resources.MeshBaseline.Bytes)),
            ("first-deformed", tasks.Any(task => task.Callback == 0x89AAE0 && task.ReadWord(0x94) != 0)),
            ("stage-peak", snapshot.NativeStage?.Groups.All(group => group.ColorDepth == 1024) == true),
            ("fourth-mesh-morph", tasks.Any(task => task.Callback == 0x89C270 && task.ReadDword(0x4C) == 0x161F750) && Changed(memory.Bio22FourthGeometry, resources.FourthMeshBaseline.Bytes)),
            ("palette-live", tasks.Any(task => task.Callback == 0x89CA70 && task.ReadByte(0x29) == 1) && memory.Bio22Palette is { } palette && !palette.Bytes.SequenceEqual(resources.PaletteBaseline.Data)),
            ("particle-live", tasks.Any(task => (task.Callback is 0x89C930 or 0x89BD20 or 0x89BF20) && task.ReadByte(0x29) == 1 && task.ReadWord(0x50) > 0)),
            ("later-particle-live", tasks.Any(task => (task.Callback is 0x89CE60 or 0x89D120) && task.ReadByte(0x29) == 1 && task.ReadWord(0x50) > 0)),
            ("final-actor-live", tasks.Any(task => (task.Callback is 0x89D3D0 or 0x89D460) && task.ReadWord(0x24) > 0)),
            ("stage-falling", tasks.Any(task => task.Callback == 0x89B8A0 && task.ReadByte(0x29) == 4) && snapshot.NativeStage?.Groups.Any(group => group.ColorDepth is > 0 and < 1024) == true)
        ];
        return candidates.Where(item => item.Ready && required.Contains(item.Name)).Select(item => item.Name).FirstOrDefault();
    }

    private static bool Changed(NativeBattleBio22Geometry? geometry, ImmutableArray<byte> baseline) => geometry is not null && !geometry.Bytes.SequenceEqual(baseline);
    private static bool IsParticle(NativeBattleModulePoolTask task) => task.Callback is 0x89C930 or 0x89BD20 or 0x89BF20 or 0x89CE60 or 0x89D120;
    private static NativeBattleModulePoolTask[] Tasks(NativeBattleOriginalEffectState? effect) => effect is null ? [] : effect.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0).ToArray();
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 22 } effect ? effect : null;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(index => battle.NativeGraphics.Descriptors.Packets[index].Command == 104);
    private sealed record Boundary(string Name, long Before, long After, string RgbaSha256);
}
