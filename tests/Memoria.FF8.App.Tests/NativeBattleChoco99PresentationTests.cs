using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleChoco99PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_ChocoMeteor_runs_original_IO_models_nodes_programs_contact_and_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO99_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_CHOCO99_SNAPSHOT to a complete controlled seven-HP queued Choco component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO99_OUTPUT") ?? throw new InvalidDataException("A distinct output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO99_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 2048;
        Assert.True(maximum > 0);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        int queued = Assert.Single(source.NativeGraphics!.Descriptors.Queue, slot => source.NativeGraphics.Descriptors.Packets[slot].Command == 104);
        uint offset = unchecked(source.NativeGraphics.Descriptors.Packets[queued].Argument - 0x1D280C4u);
        Assert.True(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(offset / 20))];
        Assert.Equal((byte)240, header.ReadByte(1));
        Assert.Equal((ushort)4, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)99, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.Equal((ushort)99, Assert.Single(assets.NativeActions, action => action.Command == 240 && action.ActionId == 4).MagicId);
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((ushort)100, source.NativePartyPresentation!.Rows[0]!.HitPoints);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([99])[98];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[98]));
        NativeBattleChoco99Resources resources = installed.Choco99Resources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        HashSet<string> requested = ["io-pending", "model-pose0", "static-node-live", "program471-uv", "program474-live", "camera-mode1", "meteo-visible", "moving476-trail", "radial-live", "small476-live", "held483-live", "secondary480-live", "loaded479-node-live", "overlay-live", "contact-before", "common-io"];
        List<Boundary> boundaries = [];
        List<short> files = [];
        HashSet<uint> barriers = [];
        HashSet<byte> poses = [];
        HashSet<byte> cameraPhases = [];
        HashSet<ushort> modes = [];
        HashSet<ushort> spriteFrames = [];
        HashSet<short> mutatedPrograms = [];
        HashSet<uint> observedPrograms = [];
        HashSet<uint> particleKinds = [];
        HashSet<uint> secondaryKinds = [];
        HashSet<ushort> cameraYIndices = [];
        bool sawMode1 = false;
        bool sawInitialProgram = false;
        bool sawOverlay = false;
        bool sawHiddenEnvironment = false;
        int particlePeak = 0;
        HashSet<uint> relocatedNodes = [];
        HashSet<uint> mutatedNodes = [];
        HashSet<uint> morphedNodes = [];
        int callbacks = 0;
        int completions = 0;
        int contacts = 0;
        int fileCompletions = 0;
        int stagePeak = 0;
        bool commonIo = false;
        bool visible = false;
        bool initialized = false;
        bool sawEnemyCentroid = false;
        uint observedCrtAdvances = 0;
        short? vibrationHandle = null;
        bool sawVibration = false;
        NativeCrtRandomState? crtStart = null;
        NativeCrtRandomState? crtEnd = null;
        BattleSnapshot prior = source;
        NativeBattleRestorationPresentationTests.Publish(output, "initial", source, null);
        try
        {
            NativeBattleRestorationPresentationTests.Publish(output, "initial-rendered", source, scene.Render(battle, 320, 224));
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The diagnostic callback limit is not a native effect duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                if (crtStart is null && before?.ReturnedDescriptor is > 0)
                {
                    crtStart = prior.NativeCrtRandom;
                }
                string? name = BoundaryName(prior, requested, resources);
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
                NativeBattleOriginalEffectState? after = Selected(current);
                initialized |= after?.ReturnedDescriptor is > 0;
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                    crtEnd = current.NativeCrtRandom;
                }
                if (before?.ReturnedDescriptor is > 0)
                {
                    observedCrtAdvances = unchecked(observedCrtAdvances + SeedDistance(prior.NativeCrtRandom!.Seed, current.NativeCrtRandom!.Seed));
                }
                ushort previousHp = prior.NativePartyPresentation!.Rows[0]!.HitPoints;
                ushort hp = current.NativePartyPresentation!.Rows[0]!.HitPoints;
                if (previousHp != hp)
                {
                    Assert.Equal((ushort)100, previousHp);
                    Assert.Equal((ushort)93, hp);
                    contacts++;
                }
                Assert.True(hp is 100 or 93);
                NativeBattleChoco99State? state = current.NativeEffectMemory?.Choco99;
                NativeBattleChoco99IoState? previousIo = prior.NativeEffectMemory?.Choco99?.Io;
                if (state?.Io is { } io)
                {
                    for (int row = 0; row < resources.Io.Script.Data.Length; row += 16)
                    {
                        uint address = NativeBattleChoco99IoState.ScriptAddress + (uint)row;
                        if (U16(resources.Io.Script.Data, row) == 0 && io.CursorAddress > address)
                        {
                            barriers.Add(address);
                        }
                    }
                    if (io.Busy != 0 && previousIo?.Busy is not > 0)
                    {
                        int cursor = checked((int)(io.CursorAddress - NativeBattleChoco99IoState.ScriptAddress));
                        short id = checked((short)U16(io.Script, cursor + 2));
                        ushort opcode = U16(io.Script, cursor);
                        uint destination = U32(io.Script, cursor + 12);
                        NativeBattleEffectFileAsset file = Assert.Single(resources.Io.Files, item => item.FileId == id);
                        byte[] copied = opcode == 1 ? current.NativeEffectMemory!.Arena!.Read(checked((int)(destination - 0x20DFAB8)), file.Data.Length) : io.Banks.Read(destination, file.Data.Length);
                        Assert.Equal(file.Data.ToArray(), copied);
                        Assert.Contains(current.NativeFileIo!.Rows, row => row.SchedulerCallback != 0 && row.CompletionCallback is 0x718510 or 0x718570 or 0x718580 or 0x718590);
                        files.Add(id);
                    }
                    if (previousIo?.Busy is > 0 && io.Busy == 0)
                    {
                        fileCompletions++;
                    }
                }
                if (state?.Context is { } context)
                {
                    modes.Add(context.Word(0x40));
                    if (Tasks(after, NativeBattleChoco99Director.Callback).Any(task => task.ReadByte(0x29) > 0))
                    {
                        vibrationHandle = unchecked((short)context.Word(0x52));
                        sawVibration |= current.NativeVibrationProcess!.IsPlaying(vibrationHandle.Value);
                    }

                    if (!sawEnemyCentroid && Tasks(after, 0x718130).Any())
                    {
                        NativeBattleVector[] enemies = current.NativeGraphics.Models.Skip(3).Where(model => (model.Flags0 & 2) != 0).Select(model => model.Transform!.Position28).ToArray();
                        short x = enemies.Length == 0 ? (short)0 : (short)((enemies.Min(point => point.X) + enemies.Max(point => point.X)) / 2);
                        short z = enemies.Length == 0 ? (short)0 : (short)((enemies.Min(point => point.Z) + enemies.Max(point => point.Z)) / 2);
                        Assert.Equal((ushort)0, context.Word(0));
                        Assert.Equal((ushort)0, context.Word(2));
                        Assert.Equal((ushort)2048, context.Word(4));
                        Assert.Equal((ushort)0, context.Word(6));
                        Assert.Equal(unchecked((ushort)Math.Max(0, (int)x)), context.Word(8));
                        Assert.Equal((ushort)0, context.Word(10));
                        Assert.Equal(unchecked((ushort)z), context.Word(12));
                        Assert.Equal((ushort)0, context.Word(14));
                        sawEnemyCentroid = true;
                    }
                }
                foreach (NativeBattleModulePoolTask model in Tasks(after, 0x71E8D0, false))
                {
                    if (state?.Models.Values.Any(owner => owner.DecodedPose is not null) == true)
                    {
                        poses.Add(model.ReadByte(0x9C));
                    }
                }
                foreach (NativeBattleModulePoolTask camera in Tasks(after, NativeBattleChoco99CameraTask.Callback, false))
                {
                    cameraPhases.Add(camera.ReadByte(0x29));
                }
                foreach (NativeBattleModulePoolTask sprite in Tasks(after, 0x71EE40))
                {
                    if ((sprite.ReadByte(0x26) & 4) == 0)
                    {
                        spriteFrames.Add(sprite.ReadWord(0x50));
                    }
                }
                if (state is not null)
                {
                    ObserveNodes(state, resources, relocatedNodes, mutatedNodes, morphedNodes);
                    foreach (uint callback in LoadedCallbacks)
                    {
                        if (Tasks(after, callback).Any())
                        {
                            observedPrograms.Add(callback);
                        }
                    }
                    foreach (short id in new short[] { 471, 474 })
                    {
                        if (Tasks(after, id == 471 ? 0x71F950u : 0x71FDC0u).Any() && LoadedVChanged(state, resources, id))
                        {
                            mutatedPrograms.Add(id);
                        }
                    }
                    sawInitialProgram |= Tasks(after, 0x7189B0).Any();
                    sawMode1 |= state.Camera is { } retainedCamera && U16(retainedCamera.Data, 0xCE) == 1;
                }
                NativeBattleModulePoolTask[] particles = Tasks(after, NativeBattleChoco99Particles.TrailCallback).Concat(Tasks(after, NativeBattleChoco99Particles.RadialCallback)).ToArray();
                particlePeak = Math.Max(particlePeak, particles.Length);
                if (after is not null)
                {
                    NativeBattleEffectHierarchy hierarchy = new(after.Pools);
                    foreach (NativeBattleModulePoolTask particle in particles)
                    {
                        particleKinds.Add(particle.Callback);
                        NativeBattleModulePoolTask parentTask = hierarchy.Read(particle.ReadDword(0x18));
                        Assert.Contains(parentTask.Callback, new uint[] { 0x71FFB0, 0x720610 });
                        Assert.True(parentTask.ReadByte(0x28) > 0);
                    }
                }
                foreach (uint callback in new uint[] { 0x7211D0, 0x721390 })
                {
                    if (Tasks(after, callback).Any())
                    {
                        secondaryKinds.Add(callback);
                    }
                }
                foreach (NativeBattleModulePoolTask cameraY in Tasks(before, 0x71F880))
                {
                    if (cameraY.ReadByte(0x29) == 1 && unchecked((short)cameraY.ReadWord(0x38)) is >= 0 and < 8)
                    {
                        ushort index = cameraY.ReadWord(0x38);
                        short impulse = resources.Meteo.CameraY.Values[index];
                        NativeBattleCameraState cameraState = current.NativeCamera ?? throw new InvalidDataException("The view translation lost its current camera owner.");
                        NativeBattleActionMatrix freshView = NativeBattlePoseMatrices.CameraView(cameraState.CurrentPose ?? throw new InvalidDataException("The view translation requires the fresh camera pose."));
                        Assert.Equal(unchecked(freshView.Y + impulse), cameraState.Projection!.View!.Y);
                        Assert.Equal((short?)0, cameraState.ViewTranslationScratch!.Y);
                        cameraYIndices.Add(index);
                    }
                }
                sawOverlay |= current.NativeGraphics.PrimitiveFrame?.LastCommands.Any(item => item.Command.Packet.Bank == NativeBattleFullscreenOverlay.PacketBank) == true;
                sawHiddenEnvironment |= current.NativeStage!.Groups.All(group => (group.Flags & 2) == 0) && current.NativeGraphics.Models.Skip(3).Take(4).Any(model => (model.Flags0 & 6) == 6);
                stagePeak = Math.Max(stagePeak, current.NativeStage!.Groups.Max(group => group.ColorDepth));
                commonIo |= CommonIo(current);
                visible |= current.NativeGraphics.PrimitiveFrame?.LastCommands.Any(item => (item.Command.Opcode & 0xFC) is 0x24 or 0x2C or 0x34 or 0x3C) == true;
                if (cold is not null)
                {
                    cold.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold.Capture()));
                    byte[] rgba = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(rgba, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output, name + "-after", current, rgba);
                    boundaries.Add(new(name!, prior.NativeGraphics.Callback, current.NativeGraphics.Callback, Convert.ToHexString(SHA256.HashData(rgba))));
                    requested.Remove(name!);
                }
            }
            Assert.True(initialized);
            Assert.Empty(requested);
            Assert.Equal(16, boundaries.Count);
            Assert.Equal(1, completions);
            Assert.Equal(1, contacts);
            Assert.Equal(Enumerable.Range(468, 21).Select(value => (short)value).ToArray(), files.ToArray());
            Assert.Equal(21, fileCompletions);
            Assert.Equal(4, barriers.Count);
            Assert.Equal(Enumerable.Range(0, 5).Select(value => (byte)value).ToArray(), poses.Order().ToArray());
            Assert.All(Enumerable.Range(1, 9).Select(value => (byte)value), phase => Assert.Contains(phase, cameraPhases));
            Assert.Equal(Enumerable.Range(0, 20).Select(value => (ushort)value).ToArray(), spriteFrames.Order().ToArray());
            Assert.All(Enumerable.Range(0, 12).Select(value => (ushort)value), mode => Assert.Contains(mode, modes));
            Assert.Equal(new[] { NativeBattleChoco99Nodes.StaticAddress, NativeBattleChoco99Nodes.LoadedAddress }.Order(), relocatedNodes.Order());
            Assert.Empty(mutatedNodes);
            Assert.Equal(new[] { NativeBattleChoco99Nodes.LoadedAddress }, morphedNodes.Order());
            Assert.Equal(new short[] { 471, 474 }, mutatedPrograms.Order());
            Assert.Equal(LoadedCallbacks.Order(), observedPrograms.Order());
            Assert.Equal(new uint[] { NativeBattleChoco99Particles.TrailCallback, NativeBattleChoco99Particles.RadialCallback }.Order(), particleKinds.Order());
            Assert.Equal(new uint[] { 0x7211D0, 0x721390 }, secondaryKinds.Order());
            Assert.Equal(Enumerable.Range(0, 8).Select(index => (ushort)index), cameraYIndices.Order());
            Assert.True(sawMode1 && sawOverlay && sawHiddenEnvironment && sawInitialProgram);
            Assert.True(particlePeak > 0);
            Assert.True(sawVibration);
            Assert.True(commonIo && visible);
            Assert.True(sawEnemyCentroid);
            Assert.NotNull(crtStart);
            Assert.NotNull(crtEnd);
            Assert.True(observedCrtAdvances > 0);
            Assert.Equal(observedCrtAdvances, SeedDistance(crtStart!.Seed, crtEnd!.Seed));
            Assert.True(stagePeak >= 1536);
            Assert.All(battle.NativeStage!.Groups, group =>
            {
                Assert.Equal((short)0, group.ColorDepth);
                Assert.Equal(0u, group.ColorFlags & 0xFFFFFF);
            });
            Assert.Equal(source.NativeStage!.Groups.Select(group => group.ColorFlags & 0xFF000000u).ToArray(), battle.NativeStage.Groups.Select(group => group.ColorFlags).ToArray());
            Assert.Equal(source.NativeStage.Groups.Select(group => (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2))).ToArray(), battle.NativeStage.Groups.Select(group => group.Flags).ToArray());
            NativeBattleChoco97Context finalContext = battle.NativeEffectMemory!.Choco99!.Context!;
            for (int actor = 3; actor < 7; actor++)
            {
                NativeBattleGraphicsModel model = battle.NativeGraphics!.Models[actor];
                if ((model.Flags0 & 2) != 0)
                {
                    Assert.Equal((ushort)(U32(finalContext.Data, 0x2C + (actor - 3) * 4) & 4), (ushort)(model.Flags0 & 4));
                }
            }
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.Equal((ushort)0, battle.NativeEffectMemory.Choco99!.Io!.Busy);
            Assert.Equal((ushort)93, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
            Assert.All(new[] { resources.SoundInitial, resources.SoundModel, resources.SoundHide, resources.SoundRestore }, sound => Assert.Contains(heard, item => item.Sound == sound));
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
            BattleSession restored = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            restored.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224).ToArray());
            Assert.False(HasAction(battle));
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
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Scenario = "Controlled queued command240/action4/effect99 party sevenHP damage component; formula/admission separate", Callbacks = callbacks, Completions = completions, ContactHpTransitions = contacts, Files = files, FileCompletions = fileCompletions, ReleasedBarriers = barriers.Order(), Modes = modes.Order(), ModelPoses = poses.Order(), CameraObservedPhases = cameraPhases.Order(), SpriteFrames = spriteFrames.Order(), RelocatedNodeBanks = relocatedNodes.Order(), MutatedUNodeBanks = mutatedNodes.Order(), MorphedXYZNodeBanks = morphedNodes.Order(), MutatedVPrograms = mutatedPrograms.Order(), ObservedLoadedPrograms = observedPrograms.Order(), StagePeak = stagePeak, ParticleKinds = particleKinds.Order(), ParticlePeak = particlePeak, SecondaryCallbacks = secondaryKinds.Order(), CameraYDeliveredIndices = cameraYIndices.Order(), SawMode1 = sawMode1, SawInitialProgram = sawInitialProgram, VibrationHandle = vibrationHandle, SawVibrationOwned = sawVibration, VibrationStillPlaying = vibrationHandle is short handle && battle.Capture().NativeVibrationProcess!.IsPlaying(handle), SawStaticOverlayPackets = sawOverlay, SawHiddenEnvironment = sawHiddenEnvironment, SawEnemyCentroidIndependentOfPartyContact = sawEnemyCentroid, SawActualCommonIo = commonIo, ObservedCrtAdvances = observedCrtAdvances, CrtObservation = "Measured shared CRT seed distance including node and actual emitter calls; independent component tests cover trail24/radial13/two-child6 recipes, not a wholecast birth-history proof", CrtStart = crtStart, CrtEnd = crtEnd, Boundaries = boundaries, SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native visual comparison", "Ordinary command admission and original damage formula", "Other targets/headers", "Hardware audibility and vibration" } }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static readonly uint[] LoadedCallbacks = [0x71F950, 0x71FDC0, 0x71FEC0, 0x71FFB0, 0x7201F0, 0x720320, 0x720410, 0x720610];

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested, NativeBattleChoco99Resources resources)
    {
        NativeBattleOriginalEffectState? effect = Selected(snapshot);
        NativeBattleChoco99State? state = snapshot.NativeEffectMemory?.Choco99;
        bool Live(uint callback, int? phase = null) => Tasks(effect, callback).Any(task => phase is null || task.ReadByte(0x29) == phase);
        string? candidate = requested.Contains("contact-before") && state?.Context?.Word(0x48) == 1 && Live(0x718130, 1) ? "contact-before"
            : requested.Contains("io-pending") && state?.Io?.Busy is > 0 ? "io-pending"
            : requested.Contains("model-pose0") && Tasks(effect, 0x71E8D0).Any(task => task.ReadByte(0x9C) == 0 && task.ReadByte(0x29) == 1) ? "model-pose0"
            : requested.Contains("static-node-live") && NodeLive(NativeBattleChoco99Nodes.StaticAddress) ? "static-node-live"
            : requested.Contains("program471-uv") && state is not null && Live(0x71F950) && LoadedVChanged(state, resources, 471) ? "program471-uv"
            : requested.Contains("program474-live") && Live(0x71FDC0, 1) ? "program474-live"
            : requested.Contains("camera-mode1") && state?.Camera is { } camera && U16(camera.Data, 0xCE) == 1 ? "camera-mode1"
            : requested.Contains("meteo-visible") && Tasks(effect, 0x720500).Any(task => task.ReadByte(0x29) == 2 && (task.ReadByte(0x26) & 4) == 0) ? "meteo-visible"
            : requested.Contains("moving476-trail") && Live(0x71FFB0, 1) && Live(NativeBattleChoco99Particles.TrailCallback) ? "moving476-trail"
            : requested.Contains("radial-live") && Live(NativeBattleChoco99Particles.RadialCallback) ? "radial-live"
            : requested.Contains("small476-live") && Live(0x720610, 1) ? "small476-live"
            : requested.Contains("held483-live") && Live(0x720320, 1) ? "held483-live"
            : requested.Contains("secondary480-live") && Live(0x7211D0, 1) ? "secondary480-live"
            : requested.Contains("loaded479-node-live") && NodeLive(NativeBattleChoco99Nodes.LoadedAddress) ? "loaded479-node-live"
            : requested.Contains("overlay-live") && Tasks(effect, 0x8DDC30).Any(task => task.ReadByte(0x29) == 1 && task.ReadWord(0x3A) > 0) ? "overlay-live"
            : requested.Contains("common-io") && CommonIo(snapshot) ? "common-io" : null;
        return candidate;

        bool NodeLive(uint address) => Tasks(effect, NativeBattleChoco99Nodes.Callback).Any(task => task.ReadDword(0x30) == address && task.ReadByte(0x29) == 1 && (task.ReadWord(0x48) > 0 || task.ReadWord(0x4A) > 0));
    }

    private static void ObserveNodes(NativeBattleChoco99State state, NativeBattleChoco99Resources resources, HashSet<uint> relocated, HashSet<uint> mutated, HashSet<uint> morphed)
    {
        foreach (NativeBattleChoco97NodeResource resource in resources.Nodes)
        {
            ImmutableArray<byte>? current = resource.Address == NativeBattleChoco99Nodes.StaticAddress ? state.Nodes.StaticData : ReadKnownBank(state.Io, resource.Address, resource.Data.Length);
            if (current is not { } data || U32(data, 0) == 0)
            {
                continue;
            }
            byte[] expected = ExpectedNodeRelocation(resource);
            bool[] mutable = new bool[expected.Length];
            if (resource.Address == NativeBattleChoco99Nodes.LoadedAddress)
            {
                foreach (int model in new[] { 5, 6 })
                {
                    int mesh = checked((int)U32(resource.Data, 0x84 + model * 4));
                    Assert.Equal(model == 5 ? 0x2DE0 : 0x38A8, mesh);
                    Assert.Equal(100u, U32(resource.Data, mesh + 4));
                    for (int vertex = 0; vertex < 100; vertex++)
                    {
                        Array.Fill(mutable, true, mesh + 8 + vertex * 8, 6);
                    }
                }
            }
            for (int offset = 0; offset < expected.Length; offset++)
            {
                if (mutable[offset])
                {
                    if (expected[offset] != data[offset])
                    {
                        morphed.Add(resource.Address);
                    }
                }
                else
                {
                    Assert.Equal(expected[offset], data[offset]);
                }
            }
            relocated.Add(resource.Address);
        }
        Assert.Empty(mutated);
    }

    private static byte[] ExpectedNodeRelocation(NativeBattleChoco97NodeResource resource)
    {
        byte[] expected = resource.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(expected, 1);
        for (int index = 0; index < 64; index++)
        {
            uint relative = U32(resource.Data, 4 + index * 4);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + index * 4), relative == 0 ? 0 : resource.Address + relative);
        }
        for (int index = 0; index < 16; index++)
        {
            uint node = U32(resource.Data, 4 + index * 4);
            if (node == 0)
            {
                continue;
            }
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                int offset = checked((int)node + field);
                BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(offset), unchecked(U32(resource.Data, offset) + resource.Address));
            }
        }
        return expected;
    }

    private static bool LoadedVChanged(NativeBattleChoco99State state, NativeBattleChoco99Resources resources, short id)
    {
        uint address = id == 471 ? 0x19E3018u : 0x19EB018u;
        ImmutableArray<byte> original = resources.Io.Files.Single(file => file.FileId == id).Data;
        if (ReadKnownBank(state.Io, address, original.Length) is not { } current)
        {
            return false;
        }
        int[] geometries = id == 471 ? [0, 3] : [2];
        return geometries.Any(geometry => TextureBytesChanged(original, current, checked((int)U32(original, 8 + geometry * 4)), false));
    }

    private static ImmutableArray<byte>? ReadKnownBank(NativeBattleChoco99IoState? io, uint address, int length)
    {
        if (io is null)
        {
            return null;
        }
        long offset = (long)address - io.Banks.BaseAddress;
        if (offset < 0 || offset > io.Banks.Length - length || io.Banks.Bytes.UnknownRegions.Any(region => region.Offset < offset + length && region.Offset + (long)region.Length > offset))
        {
            return null;
        }
        return io.Banks.Read(address, length).ToImmutableArray();
    }

    private static bool TextureBytesChanged(ImmutableArray<byte> original, ImmutableArray<byte> current, int mesh, bool u)
    {
        int cursor = checked(mesh + (int)U32(original, mesh) / 4 * 4);
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)U32(original, cursor));
            cursor += 4;
            int[] corners = group is 2 or 6 ? [10, 12, 16] : group is 3 or 7 ? [12, 16, 20, 22] : [];
            for (int face = 0; face < count; face++)
            {
                foreach (int corner in corners)
                {
                    int offset = cursor + corner + (u ? 0 : 1);
                    if (original[offset] != current[offset])
                    {
                        return true;
                    }
                }
                cursor += lengths[group];
            }
        }
        return false;
    }

    private static uint SeedDistance(uint from, uint to)
    {
        uint multiplier = 214013;
        uint increment = 2531011;
        uint result = 0;
        for (uint bit = 1; bit != 0; bit <<= 1)
        {
            if ((from & bit) != (to & bit))
            {
                from = unchecked(from * multiplier + increment);
                result |= bit;
            }
            increment = unchecked((multiplier + 1) * increment);
            multiplier = unchecked(multiplier * multiplier);
        }
        Assert.Equal(to, from);
        return result;
    }

    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static bool CommonIo(BattleSnapshot snapshot) => snapshot.NativeFileIo?.Rows.Any(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470) == true;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 99, Binding.Entry: 0x717D30 } effect ? effect : null;
    private static IEnumerable<NativeBattleModulePoolTask> Tasks(NativeBattleOriginalEffectState? effect, uint callback, bool active = true) => effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (!active || (task.Flags & 1) != 0) && task.Callback == callback) ?? [];
    private sealed record Boundary(string Name, long Before, long After, string RgbaSha256);
}
