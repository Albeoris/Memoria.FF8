using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleChoco100PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_ChocoBuckle_runs_original_IO_models_nodes_programs_contact_and_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO100_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_CHOCO100_SNAPSHOT to a complete controlled seven-HP queued Choco component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO100_OUTPUT") ?? throw new InvalidDataException("A distinct output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO100_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 2048;
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
        Assert.Equal((ushort)5, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)100, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.Equal((ushort)100, Assert.Single(assets.NativeActions, action => action.Command == 240 && action.ActionId == 5).MagicId);
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((ushort)100, source.NativePartyPresentation!.Rows[0]!.HitPoints);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([100])[99];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[99]));
        NativeBattleChoco100Resources resources = installed.Choco100Resources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        HashSet<string> requested = ["io-pending", "static-model", "static-node", "program492-uv", "program496-live", "loaded495-node", "loaded-reveal", "loaded-y", "loaded-pose1", "loaded-fade", "loaded501-node", "program502-live", "camera-late", "wave-live", "contact-before", "common-io"];
        List<Boundary> boundaries = [];
        List<short> files = [];
        HashSet<uint> waits = [];
        HashSet<byte> poses = [];
        HashSet<byte> loadedPoses = [];
        HashSet<byte> cameraPhases = [];
        HashSet<byte> wavePhases = [];
        HashSet<ushort> modes = [];
        HashSet<ushort> cameraYIndices = [];
        HashSet<uint> relocatedNodes = [];
        HashSet<uint> morphedNodes = [];
        HashSet<short> mutatedPrograms = [];
        HashSet<uint> programs = [];
        int callbacks = 0;
        int completions = 0;
        int contacts = 0;
        int fileCompletions = 0;
        int stagePeak = 0;
        int modelPeak = 0;
        bool commonIo = false;
        bool visible = false;
        bool initialized = false;
        bool loadedScratchWritten = false;
        bool staticPreservedLoadedSuffix = false;
        bool waveWorkWritten = false;
        bool sawHiddenActor = false;
        short? vibrationHandle = null;
        bool sawVibration = false;
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
                foreach (NativeBattleModulePoolTask y in Tasks(before, 0x716300).Where(task => task.ReadByte(0x29) == 1))
                {
                    cameraYIndices.Add(y.ReadWord(0x38));
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
                NativeBattleChoco100State? state = current.NativeEffectMemory?.Choco100;
                NativeBattleChoco100IoState? previousIo = prior.NativeEffectMemory?.Choco100?.Io;
                if (state?.Io is { } io)
                {
                    for (int row = 0; row < resources.Io.Script.Data.Length; row += 16)
                    {
                        uint address = NativeBattleChoco100IoState.ScriptAddress + (uint)row;
                        if (U16(resources.Io.Script.Data, row) == 0 && io.CursorAddress > address)
                        {
                            waits.Add(address);
                        }
                    }
                    if (io.Busy != 0 && previousIo?.Busy is not > 0)
                    {
                        int cursor = checked((int)(io.CursorAddress - NativeBattleChoco100IoState.ScriptAddress));
                        short id = checked((short)U16(io.Script, cursor + 2));
                        ushort opcode = U16(io.Script, cursor);
                        uint destination = U32(io.Script, cursor + 12);
                        NativeBattleEffectFileAsset file = Assert.Single(resources.Io.Files, item => item.FileId == id);
                        byte[] copied = opcode == 1 ? current.NativeEffectMemory!.Arena!.Read(checked((int)(destination - 0x20DFAB8)), file.Data.Length) : io.Banks.Read(destination, file.Data.Length);
                        Assert.Equal(file.Data.ToArray(), copied);
                        Assert.Contains(current.NativeFileIo!.Rows, row => row.SchedulerCallback != 0 && row.CompletionCallback is 0x70F130 or 0x70F190 or 0x70F1A0 or 0x70F1C0);
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
                    if (Tasks(after, NativeBattleChoco100Director.Callback).Any(task => task.ReadByte(0x29) > 0))
                    {
                        vibrationHandle = unchecked((short)context.Word(0x52));
                        sawVibration |= current.NativeVibrationProcess!.IsPlaying(vibrationHandle.Value);
                    }
                }
                foreach (NativeBattleModulePoolTask model in Tasks(after, 0x715540, false))
                {
                    if (state?.Models.Values.Any(owner => owner.DecodedPose is not null && owner.ResourceAddress == resources.Model.Model.Address) == true)
                    {
                        poses.Add(model.ReadByte(0x9C));
                    }
                }
                foreach (NativeBattleModulePoolTask model in Tasks(after, 0x7175F0, false))
                {
                    if (state?.Models.Values.Any(owner => owner.DecodedPose is not null && owner.ResourceAddress == resources.LoadedModel.Model.Address) == true)
                    {
                        loadedPoses.Add(model.ReadByte(0x9C));
                    }
                }
                if (state is not null)
                {
                    state.Validate();
                    modelPeak = Math.Max(modelPeak, state.Models.Count);
                    loadedScratchWritten |= state.ProjectedScratch.Skip(0xCD0).Any(value => value != 0);
                    if (loadedScratchWritten && !Tasks(after, 0x7175F0).Any(task => (task.ReadByte(0x26) & 4) == 0) && prior.NativeEffectMemory?.Choco100 is { } previousState && Tasks(after, 0x715540).Any(task => (task.ReadByte(0x26) & 4) == 0))
                    {
                        Assert.Equal(previousState.ProjectedScratch.Skip(0xCD0), state.ProjectedScratch.Skip(0xCD0));
                        staticPreservedLoadedSuffix = true;
                    }
                    ObserveNodes(state, resources, relocatedNodes, morphedNodes);
                    foreach (short id in new short[] { 492, 496 })
                    {
                        if (Tasks(after, id == 492 ? 0x7163D0u : 0x716840u).Any() && LoadedVChanged(state, resources, id))
                        {
                            mutatedPrograms.Add(id);
                        }
                    }
                    if (state.SecondarySettings is { } settings && Tasks(after, 0x716C20).Any(task => task.ReadByte(0x29) >= 2))
                    {
                        settings.Validate();
                        Assert.Equal(state.Io!.BufferAddress, settings.WorkAddress());
                        Assert.All(new[] { 4, 5, 6, 7, 0x14, 0x15, 0x1C, 0x1D, 0x1E, 0x24, 0x25 }, index => Assert.True(settings.Known[index]));
                        int work = checked((int)(settings.WorkAddress() - 0x20DFAB8u));
                        waveWorkWritten |= current.NativeEffectMemory!.Arena!.Read(work + 8, 8).Any(value => value != 0);
                    }
                }
                foreach (NativeBattleModulePoolTask camera in Tasks(after, NativeBattleChoco100CameraTask.Callback, false))
                {
                    cameraPhases.Add(camera.ReadByte(0x29));
                }
                foreach (NativeBattleModulePoolTask wave in Tasks(after, 0x716C20, false))
                {
                    wavePhases.Add(wave.ReadByte(0x29));
                    sawHiddenActor |= (current.NativeGraphics.Models[wave.ReadByte(0x2D)].Flags0 & 4) != 0;
                }
                foreach (uint callback in new uint[] { 0x70F5F0, 0x7163D0, 0x716840, 0x716950 })
                {
                    if (Tasks(after, callback).Any())
                    {
                        programs.Add(callback);
                    }
                }
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
            Assert.Equal(Enumerable.Range(489, 16).Select(value => (short)value).ToArray(), files.ToArray());
            Assert.Equal(16, fileCompletions);
            Assert.Equal(5, waits.Count);
            Assert.Equal(Enumerable.Range(0, 5).Select(value => (byte)value), poses.Order());
            Assert.Equal(new byte[] { 0, 1, 2 }, loadedPoses.Order());
            Assert.All(Enumerable.Range(1, 13).Select(value => (byte)value), phase => Assert.Contains(phase, cameraPhases));
            Assert.All(Enumerable.Range(1, 6).Select(value => (byte)value), phase => Assert.Contains(phase, wavePhases));
            Assert.Equal(resources.Nodes.Select(node => node.Address).Order(), relocatedNodes.Order());
            Assert.NotEmpty(morphedNodes);
            Assert.Equal(new short[] { 492, 496 }, mutatedPrograms.Order());
            Assert.Equal(new uint[] { 0x70F5F0, 0x7163D0, 0x716840, 0x716950 }.Order(), programs.Order());
            Assert.InRange(modelPeak, 1, 2);
            Assert.All(Enumerable.Range(0, 8).Select(value => (ushort)value), index => Assert.Contains(index, cameraYIndices));
            Assert.True(loadedScratchWritten);
            Assert.True(waveWorkWritten && sawHiddenActor);
            Assert.True(sawVibration && commonIo && visible);
            Assert.True(stagePeak >= 1536);
            Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.Equal((ushort)0, battle.NativeEffectMemory.Choco100!.Io!.Busy);
            Assert.Equal((ushort)93, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
            Assert.All(new[] { resources.SoundInitial, resources.SoundModel, resources.SoundDirector, resources.SoundCamera }, sound => Assert.Contains(heard, item => item.Sound == sound));
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
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Scenario = "Controlled queued command240/action5/effect100 party sevenHP component; formula/admission separate", Callbacks = callbacks, Completions = completions, ContactHpTransitions = contacts, Files = files, FileCompletions = fileCompletions, ReleasedWaits = waits.Order(), Modes = modes.Order(), StaticPoses = poses.Order(), LoadedPoses = loadedPoses.Order(), CameraPhases = cameraPhases.Order(), SecondaryPhases = wavePhases.Order(), RelocatedNodeBanks = relocatedNodes.Order(), MorphedNodeBanks = morphedNodes.Order(), MutatedVPrograms = mutatedPrograms.Order(), Programs = programs.Order(), StagePeak = stagePeak, ModelOwners = modelPeak, LoadedProjectedSuffixWritten = loadedScratchWritten, StaticPreservedLoadedSuffix = staticPreservedLoadedSuffix, SecondaryWorkWritten = waveWorkWritten, ActorHiddenDuringWave = sawHiddenActor, CameraYIndices = cameraYIndices.Order(), SawActualCommonIo = commonIo, Boundaries = boundaries, SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native visual comparison", "Ordinary command admission and original damage formula", "Other targets/headers", "Hardware audibility and vibration" } }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested, NativeBattleChoco100Resources resources)
    {
        NativeBattleOriginalEffectState? effect = Selected(snapshot);
        NativeBattleChoco100State? state = snapshot.NativeEffectMemory?.Choco100;
        bool Live(uint callback, int? phase = null) => Tasks(effect, callback).Any(task => phase is null || task.ReadByte(0x29) == phase);
        bool NodeLive(uint address) => Tasks(effect, NativeBattleChoco100Nodes.Callback).Any(task => task.ReadDword(0x30) == address && task.ReadByte(0x29) == 1 && (task.ReadWord(0x48) > 0 || task.ReadWord(0x4A) > 0));
        (string Name, bool Ready)[] candidates =
        [
            ("io-pending", state?.Io?.Busy is > 0),
            ("static-model", Live(0x715540, 1)),
            ("static-node", NodeLive(NativeBattleChoco100Nodes.StaticAddress)),
            ("program492-uv", state is not null && Live(0x7163D0) && LoadedVChanged(state, resources, 492)),
            ("program496-live", Live(0x716840, 1)),
            ("loaded495-node", NodeLive(NativeBattleChoco100Nodes.Loaded495Address)),
            ("loaded-reveal", Live(0x7175F0, 2)),
            ("loaded-y", Live(0x7175F0, 5)),
            ("loaded-pose1", Live(0x7175F0, 6)),
            ("loaded-fade", Live(0x7175F0, 8)),
            ("loaded501-node", NodeLive(NativeBattleChoco100Nodes.Loaded501Address)),
            ("program502-live", Live(0x716950, 1)),
            ("camera-late", Live(NativeBattleChoco100CameraTask.Callback, 10)),
            ("wave-live", Live(0x716C20, 2)),
            ("contact-before", state?.Context?.Word(0x48) == 1 && Live(0x70ED60, 1)),
            ("common-io", CommonIo(snapshot))
        ];
        return candidates.Where(item => item.Ready && requested.Contains(item.Name)).Select(item => item.Name).FirstOrDefault();
    }

    private static void ObserveNodes(NativeBattleChoco100State state, NativeBattleChoco100Resources resources, HashSet<uint> relocated, HashSet<uint> morphed)
    {
        foreach (NativeBattleChoco97NodeResource resource in resources.Nodes)
        {
            ImmutableArray<byte>? current = resource.Address == NativeBattleChoco100Nodes.StaticAddress ? state.Nodes.StaticData : ReadKnownBank(state.Io, resource.Address, resource.Data.Length);
            if (current is not { } data || U32(data, 0) != 1)
            {
                continue;
            }
            byte[] expected = ExpectedNodeRelocation(resource);
            bool[] xyz = new bool[expected.Length];
            for (int model = 0; model < 16; model++)
            {
                uint mesh = U32(resource.Data, 0x84 + model * 4);
                if (mesh == 0)
                {
                    continue;
                }
                int start = checked((int)mesh);
                int count = checked((int)U32(resource.Data, start + 4));
                for (int vertex = 0; vertex < count; vertex++)
                {
                    Array.Fill(xyz, true, checked(start + 8 + vertex * 8), 6);
                }
            }
            for (int index = 0; index < expected.Length; index++)
            {
                if (xyz[index])
                {
                    if (expected[index] != data[index])
                    {
                        morphed.Add(resource.Address);
                    }
                }
                else
                {
                    Assert.Equal(expected[index], data[index]);
                }
            }
            relocated.Add(resource.Address);
        }
    }

    private static bool LoadedVChanged(NativeBattleChoco100State state, NativeBattleChoco100Resources resources, short id)
    {
        NativeBattleChoco100LoadedProgram profile = id == 492 ? NativeBattleChoco100LoadedProgram.File492 : NativeBattleChoco100LoadedProgram.File496;
        uint address = NativeBattleChoco100ProgramDrawing.LoadedProfile(profile).Address;
        ImmutableArray<byte> original = resources.Io.Files.Single(file => file.FileId == id).Data;
        if (ReadKnownBank(state.Io, address, original.Length) is not { } current)
        {
            return false;
        }
        int[] geometries = id == 492 ? [0, 3] : [2];
        return geometries.Any(geometry => TextureBytesChanged(original, current, checked((int)U32(original, 8 + geometry * 4)), false));
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

    private static ImmutableArray<byte>? ReadKnownBank(NativeBattleChoco100IoState? io, uint address, int length)
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

    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static bool CommonIo(BattleSnapshot snapshot) => snapshot.NativeFileIo?.Rows.Any(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470) == true;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 100, Binding.Entry: 0x70D390 } effect ? effect : null;
    private static IEnumerable<NativeBattleModulePoolTask> Tasks(NativeBattleOriginalEffectState? effect, uint callback, bool active = true) => effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (!active || (task.Flags & 1) != 0) && task.Callback == callback) ?? [];
    private sealed record Boundary(string Name, long Before, long After, string RgbaSha256);
}
