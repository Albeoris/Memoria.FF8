using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleChoco97PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_ChocoFire_runs_original_IO_models_nodes_programs_contact_and_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO97_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_CHOCO97_SNAPSHOT to a complete controlled seven-HP queued Choco component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO97_OUTPUT") ?? throw new InvalidDataException("A distinct output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO97_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 2048;
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
        Assert.Equal((ushort)2, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)97, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.Equal((ushort)97, Assert.Single(assets.NativeActions, action => action.Command == 240 && action.ActionId == 2).MagicId);
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((ushort)100, source.NativePartyPresentation!.Rows[0]!.HitPoints);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([97])[96];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[96]));
        NativeBattleChoco97Resources resources = installed.Choco97Resources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        HashSet<string> requested = ["io-pending", "model-pose0", "static-node-live", "mode2", "mode4", "loaded-program-uv", "contact-before", "stage-fade", "camera-tail", "common-io"];
        List<Boundary> boundaries = [];
        List<short> files = [];
        HashSet<uint> barriers = [];
        HashSet<byte> poses = [];
        HashSet<byte> cameraPhases = [];
        HashSet<ushort> modes = [];
        HashSet<ushort> spriteFrames = [];
        HashSet<NativeBattleChoco97LoadedProgram> mutatedPrograms = [];
        HashSet<uint> relocatedNodes = [];
        HashSet<uint> mutatedNodes = [];
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
                NativeBattleChoco97State? state = current.NativeEffectMemory?.Choco97;
                NativeBattleChoco97IoState? previousIo = prior.NativeEffectMemory?.Choco97?.Io;
                if (state?.Io is { } io)
                {
                    for (int row = 0; row < resources.Io.Script.Data.Length; row += 16)
                    {
                        uint address = NativeBattleChoco97IoState.ScriptAddress + (uint)row;
                        if (U16(resources.Io.Script.Data, row) == 0 && io.CursorAddress > address)
                        {
                            barriers.Add(address);
                        }
                    }
                    if (io.Busy != 0 && previousIo?.Busy is not > 0)
                    {
                        int cursor = checked((int)(io.CursorAddress - NativeBattleChoco97IoState.ScriptAddress));
                        short id = checked((short)U16(io.Script, cursor + 2));
                        ushort opcode = U16(io.Script, cursor);
                        uint destination = U32(io.Script, cursor + 12);
                        NativeBattleEffectFileAsset file = Assert.Single(resources.Io.Files, item => item.FileId == id);
                        byte[] copied = opcode == 1 ? current.NativeEffectMemory!.Arena!.Read(checked((int)(destination - 0x20DFAB8)), file.Data.Length) : io.Banks.Read(destination, file.Data.Length);
                        Assert.Equal(file.Data.ToArray(), copied);
                        Assert.Contains(current.NativeFileIo!.Rows, row => row.SchedulerCallback != 0 && row.CompletionCallback is 0x72A1F0 or 0x72A250 or 0x72A260);
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
                    Assert.Equal((ushort)0, context.Word(0x4C));
                    if (!sawEnemyCentroid && Tasks(after, 0x729E20).Any())
                    {
                        NativeBattleVector[] enemies = current.NativeGraphics.Models.Skip(3).Where(model => (model.Flags0 & 2) != 0).Select(model => model.Transform!.Position28).ToArray();
                        short x = enemies.Length == 0 ? (short)0 : (short)((enemies.Min(point => point.X) + enemies.Max(point => point.X)) / 2);
                        short z = enemies.Length == 0 ? (short)0 : (short)((enemies.Min(point => point.Z) + enemies.Max(point => point.Z)) / 2);
                        Assert.Equal((ushort)0, context.Word(0));
                        Assert.Equal((ushort)0, context.Word(2));
                        Assert.Equal((ushort)2048, context.Word(4));
                        Assert.Equal((ushort)0, context.Word(6));
                        Assert.Equal(unchecked((ushort)x), context.Word(8));
                        Assert.Equal((ushort)0, context.Word(10));
                        Assert.Equal(unchecked((ushort)z), context.Word(12));
                        Assert.Equal((ushort)0, context.Word(14));
                        sawEnemyCentroid = true;
                    }
                }
                foreach (NativeBattleModulePoolTask model in Tasks(after, 0x7306A0, false))
                {
                    if (state?.Model?.DecodedPose is not null)
                    {
                        poses.Add(model.ReadByte(0x9C));
                    }
                }
                foreach (NativeBattleModulePoolTask camera in Tasks(after, NativeBattleChoco97CameraTask.Callback, false))
                {
                    cameraPhases.Add(camera.ReadByte(0x29));
                }
                foreach (NativeBattleModulePoolTask sprite in Tasks(after, 0x730C10))
                {
                    if ((sprite.ReadByte(0x26) & 4) == 0)
                    {
                        spriteFrames.Add(sprite.ReadWord(0x50));
                    }
                }
                if (state is not null)
                {
                    ObserveNodes(state, resources, relocatedNodes, mutatedNodes);
                    foreach (NativeBattleChoco97LoadedProgram profile in Enum.GetValues<NativeBattleChoco97LoadedProgram>())
                    {
                        var definition = NativeBattleChoco97ProgramDrawing.LoadedProfile(profile);
                        if (Tasks(after, definition.Callback).Any() && LoadedVChanged(state, resources, profile))
                        {
                            mutatedPrograms.Add(profile);
                        }
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
            Assert.Equal(10, boundaries.Count);
            Assert.Equal(1, completions);
            Assert.Equal(1, contacts);
            Assert.Equal(Enumerable.Range(442, 12).Select(value => (short)value).ToArray(), files.ToArray());
            Assert.Equal(12, fileCompletions);
            Assert.Equal(3, barriers.Count);
            Assert.Equal(Enumerable.Range(0, 6).Select(value => (byte)value).ToArray(), poses.Order().ToArray());
            Assert.All(Enumerable.Range(1, 7).Select(value => (byte)value), phase => Assert.Contains(phase, cameraPhases));
            Assert.Equal(Enumerable.Range(0, 20).Select(value => (ushort)value).ToArray(), spriteFrames.Order().ToArray());
            Assert.All(Enumerable.Range(0, 7).Select(value => (ushort)value), mode => Assert.Contains(mode, modes));
            Assert.Equal(2, relocatedNodes.Count);
            Assert.Equal(new[] { NativeBattleChoco97Nodes.LoadedAddress }, mutatedNodes.Order());
            Assert.Equal(3, mutatedPrograms.Count);
            Assert.True(commonIo && visible);
            Assert.True(sawEnemyCentroid);
            Assert.NotNull(crtStart);
            Assert.NotNull(crtEnd);
            Assert.True(observedCrtAdvances > 0);
            Assert.Equal(observedCrtAdvances, SeedDistance(crtStart!.Seed, crtEnd!.Seed));
            Assert.Equal(3072, stagePeak);
            Assert.All(battle.NativeStage!.Groups, group =>
            {
                Assert.Equal((short)0, group.ColorDepth);
                Assert.Equal(0u, group.ColorFlags & 0xFFFFFF);
            });
            Assert.Equal(source.NativeStage!.Groups.Select(group => group.ColorFlags & 0xFF000000u).ToArray(), battle.NativeStage.Groups.Select(group => group.ColorFlags).ToArray());
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.Equal((ushort)0, battle.NativeEffectMemory.Choco97!.Io!.Busy);
            Assert.Equal((ushort)93, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
            Assert.All(new[] { resources.SoundInitial, resources.SoundFirst, resources.SoundSecond }, sound => Assert.Contains(heard, item => item.Sound == sound));
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
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Scenario = "Controlled queued command240/action2/effect97 party sevenHP damage component; formula/admission separate", Callbacks = callbacks, Completions = completions, ContactHpTransitions = contacts, Files = files, FileCompletions = fileCompletions, ReleasedBarriers = barriers.Order(), Modes = modes.Order(), ModelPoses = poses.Order(), CameraObservedPhases = cameraPhases.Order(), SpriteFrames = spriteFrames.Order(), RelocatedNodeBanks = relocatedNodes.Order(), MutatedUNodeBanks = mutatedNodes.Order(), MutatedVPrograms = mutatedPrograms.Order(), StagePeak = stagePeak, SawEnemyCentroidIndependentOfPartyContact = sawEnemyCentroid, SawActualCommonIo = commonIo, ObservedCrtAdvances = observedCrtAdvances, CrtObservation = "Measured seed distance over the implemented unchanged node RNG family; not an independent birth-history proof", CrtStart = crtStart, CrtEnd = crtEnd, Boundaries = boundaries, SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native visual comparison", "Ordinary command admission and original damage formula", "Other targets/headers", "Hardware audibility and vibration" } }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested, NativeBattleChoco97Resources resources)
    {
        NativeBattleOriginalEffectState? effect = Selected(snapshot);
        NativeBattleChoco97State? state = snapshot.NativeEffectMemory?.Choco97;
        if (requested.Contains("contact-before") && state?.Context?.Word(0x48) == 1 && Tasks(effect, 0x729E20).Any(task => task.ReadByte(0x29) == 1))
        {
            return "contact-before";
        }
        if (requested.Contains("io-pending") && state?.Io?.Busy is > 0)
        {
            return "io-pending";
        }
        if (requested.Contains("model-pose0") && Tasks(effect, 0x7306A0).Any(task => task.ReadByte(0x29) == 1 && task.ReadByte(0x9C) == 0))
        {
            return "model-pose0";
        }
        if (requested.Contains("static-node-live") && state?.Nodes.StaticData is { } data && U32(data, 0) == 1 && Tasks(effect, NativeBattleChoco97Nodes.Callback).Any(task => task.ReadDword(0x30) == NativeBattleChoco97Nodes.StaticAddress && task.ReadByte(0x29) == 1 && task.ReadWord(0x34 + 0x14) > 0 && task.ReadWord(0x34 + 0x16) > 0))
        {
            return "static-node-live";
        }
        if (requested.Contains("mode2") && state?.Context?.Word(0x40) == 2 && Tasks(effect, 0x731840).Any())
        {
            return "mode2";
        }
        if (requested.Contains("mode4") && state?.Context?.Word(0x40) == 4 && Tasks(effect, 0x731950).Any())
        {
            return "mode4";
        }
        if (requested.Contains("loaded-program-uv") && state is not null && Tasks(effect, 0x731100).Any(task => task.ReadDword(0x98) >= 2) && LoadedVChanged(state, resources, NativeBattleChoco97LoadedProgram.File450))
        {
            return "loaded-program-uv";
        }
        if (requested.Contains("stage-fade") && Tasks(effect, 0x731B40).Any(task => task.ReadByte(0x29) == 1))
        {
            return "stage-fade";
        }
        if (requested.Contains("camera-tail") && Tasks(effect, NativeBattleChoco97CameraTask.Callback).Any(task => task.ReadByte(0x29) == 5))
        {
            return "camera-tail";
        }
        return requested.Contains("common-io") && CommonIo(snapshot) ? "common-io" : null;
    }

    private static void ObserveNodes(NativeBattleChoco97State state, NativeBattleChoco97Resources resources, HashSet<uint> relocated, HashSet<uint> mutated)
    {
        foreach (NativeBattleChoco97NodeResource resource in resources.Nodes)
        {
            ImmutableArray<byte>? current = resource.Address == NativeBattleChoco97Nodes.StaticAddress ? state.Nodes.StaticData : ReadKnownBank(state.Io, resource.Address, resource.Data.Length);
            if (current is not { } data || U32(data, 0) == 0)
            {
                continue;
            }
            for (int index = 0; index < 64; index++)
            {
                uint relative = U32(resource.Data, 4 + index * 4);
                Assert.Equal(relative == 0 ? 0u : resource.Address + relative, U32(data, 4 + index * 4));
            }
            relocated.Add(resource.Address);
            if (NodeUChanged(resource, data))
            {
                mutated.Add(resource.Address);
            }
        }
    }

    private static bool NodeUChanged(NativeBattleChoco97NodeResource resource, ImmutableArray<byte> current)
    {
        if (U32(current, 0) == 0)
        {
            return false;
        }
        int mesh = checked((int)U32(resource.Data, 0x84));
        return mesh != 0 && TextureBytesChanged(resource.Data, current, mesh, true);
    }

    private static bool LoadedVChanged(NativeBattleChoco97State state, NativeBattleChoco97Resources resources, NativeBattleChoco97LoadedProgram profile)
    {
        if (state.Io is null)
        {
            return false;
        }
        var definition = NativeBattleChoco97ProgramDrawing.LoadedProfile(profile);
        int id = profile == NativeBattleChoco97LoadedProgram.File445 ? 445 : profile == NativeBattleChoco97LoadedProgram.File449 ? 449 : 450;
        ImmutableArray<byte> original = resources.Io.Files.Single(file => file.FileId == id).Data;
        if (ReadKnownBank(state.Io, definition.Address, definition.Length) is not { } current)
        {
            return false;
        }
        return definition.Geometries.Any(geometry => TextureBytesChanged(original, current, checked((int)U32(original, 8 + geometry * 4)), false));
    }

    private static ImmutableArray<byte>? ReadKnownBank(NativeBattleChoco97IoState? io, uint address, int length)
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
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 97, Binding.Entry: 0x729A60 } effect ? effect : null;
    private static IEnumerable<NativeBattleModulePoolTask> Tasks(NativeBattleOriginalEffectState? effect, uint callback, bool active = true) => effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (!active || (task.Flags & 1) != 0) && task.Callback == callback) ?? [];
    private sealed record Boundary(string Name, long Before, long After, string RgbaSha256);
}
