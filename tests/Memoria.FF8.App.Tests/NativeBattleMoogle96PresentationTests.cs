using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMoogle96PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_Moogle_healing_completes_original_resources_and_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOOGLE96_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_MOOGLE96_SNAPSHOT to a complete queued controlled post-formula Moogle healing fixture.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOOGLE96_OUTPUT") ?? throw new InvalidDataException("A distinct MEMORIA_FF8_MOOGLE96_OUTPUT directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(), "Output must be a distinct empty directory.");
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOOGLE96_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 1024;
        Assert.True(maximum > 0);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        int queued = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
        uint offset = unchecked(graphics.Descriptors.Packets[queued].Argument - 0x1D280C4u);
        Assert.True(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        int headerIndex = checked((int)(offset / 20));
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[headerIndex];
        Assert.Equal((byte)240, header.ReadByte(1));
        Assert.Equal((ushort)6, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)96, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.True(graphics.ActionGraphics is null || graphics.ActionGraphics.HeaderIndex == headerIndex);
        Assert.Equal((ushort)96, Assert.Single(assets.NativeActions, action => action.Command == 240 && action.ActionId == 6).MagicId);
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal(1, contact.Primary.HitFlags3 & 1);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((byte)0, graphics.Models[0].ModelId4);
        Assert.Equal((ushort)100, source.NativePartyPresentation!.Rows[0]!.HitPoints);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([96])[95];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[95]));
        NativeBattleMoogle96Resources resources = installed.Moogle96Resources!;
        uint transition = source.NativeStage!.ModelColors!.Color40;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        int sourceEvents = battle.Events.Count;
        HashSet<string> requested = ["io-pending", "mode-handoff", "program-uv-live", "pose0", "pose1-repeat", "pose2", "camera-late", "nodes-second"];
        List<Boundary> boundaries = [];
        List<short> fileIds = [];
        HashSet<short> modes = [];
        HashSet<byte> modelPoses = [];
        HashSet<byte> cameraPhases = [];
        HashSet<string> cameras = [];
        int callbacks = 0;
        int capturedGeometry = 0;
        int contacts = 0;
        int completions = 0;
        int fileCompletions = 0;
        int stagePeak = 0;
        short? vibrationHandle = null;
        bool initialized = false;
        bool sawVibration = false;
        bool sawMusicZero = false;
        bool sawPrivate = false;
        bool sawCommonNodeSprites = false;
        NativeBattlePrimitiveReference? directorPacket = null;
        long? directorPacketCallback = null;
        bool sawMutableUv = false;
        bool sawTwoBanks = false;
        bool sawEnemyRestoreWithOverride = false;
        bool stageCleared = false;
        BattleSnapshot prior = source;
        NativeBattleRestorationPresentationTests.Publish(output, "initial", source, null);
        try
        {
            NativeBattleRestorationPresentationTests.Publish(output, "initial-rendered", source, scene.Render(battle, 320, 224));
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The diagnostic callback budget was exceeded; it is not a native effect duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? beforeEffect = Selected(prior);
                string? name = BoundaryName(prior, requested, resources);
                BattleSession? cold = null;
                if (name is not null)
                {
                    NativeBattleRestorationPresentationTests.Publish(output, name + "-before", prior, scene.Render(battle, 320, 224));
                    cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
                    Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                }
                battle.AdvanceNativeGraphicsPresentation(Callback);
                callbacks++;
                audio.Update(battle);
                BattleSnapshot current = battle.Capture();
                capturedGeometry += NativeBattleRestorationSummonCaptureAssertions.AssertOwned(current, 96);
                Assert.Equal(prior.NativeGraphics!.Callback + 1, current.NativeGraphics!.Callback);
                NativeBattleOriginalEffectState? afterEffect = Selected(current);
                initialized |= afterEffect?.ReturnedDescriptor is > 0;
                if (beforeEffect?.ReturnedDescriptor is > 0 && afterEffect?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                ushort beforeHp = prior.NativePartyPresentation!.Rows[0]!.HitPoints;
                ushort hp = current.NativePartyPresentation!.Rows[0]!.HitPoints;
                if (beforeHp != hp)
                {
                    Assert.Equal((ushort)100, beforeHp);
                    Assert.Equal((ushort)107, hp);
                    contacts++;
                }
                Assert.True(hp is 100 or 107);
                for (int actor = 0; actor < graphics.Models.Length; actor++)
                {
                    Assert.Equal(graphics.Models[actor].ActiveStagedResourceRow100, current.NativeGraphics.Models[actor].ActiveStagedResourceRow100);
                }
                NativeBattleMoogle96State? state = current.NativeEffectMemory?.Moogle96;
                NativeBattleMoogle96IoState? previousIo = prior.NativeEffectMemory?.Moogle96?.Io;
                if (state?.Io is { Busy: not 0 } io)
                {
                    int cursor = checked((int)(io.CursorAddress - NativeBattleMoogle96IoState.ScriptAddress));
                    short fileId = checked((short)BinaryPrimitives.ReadUInt16LittleEndian(io.Script.AsSpan().Slice(cursor + 2)));
                    NativeBattleEffectFileAsset file = Assert.Single(resources.Files, file => file.FileId == fileId);
                    Assert.Equal(file.Data.ToArray(), current.NativeEffectMemory!.Arena!.Read(checked((int)(io.BufferAddress - 0x20DFAB8)), file.Data.Length));
                    Assert.Contains(current.NativeFileIo!.Rows, row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x732590);
                    if (previousIo?.Busy is not > 0)
                    {
                        fileIds.Add(fileId);
                    }
                }
                if (previousIo?.Busy is > 0 && state?.Io?.Busy == 0)
                {
                    fileCompletions++;
                }
                if (state?.DogControl is { } dog)
                {
                    modes.Add(Word(dog, 0x40));
                    short handle = Word(dog, 0x52);
                    if (handle > 0)
                    {
                        vibrationHandle = handle;
                        sawVibration |= current.NativeVibrationProcess!.IsPlaying(handle);
                    }
                }
                foreach (NativeBattleModulePoolTask model in Tasks(afterEffect, 0x739610))
                {
                    modelPoses.Add(model.ReadByte(0x9C));
                    Assert.Equal(30, Assert.Single(state!.Models).DecodedPose!.Bones.Length);
                }
                foreach (NativeBattleModulePoolTask camera in Tasks(afterEffect, 0x732A00))
                {
                    cameraPhases.Add(camera.ReadByte(0x29));
                }
                if (state?.Camera is not null)
                {
                    cameras.Add(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(current.NativeCamera))));
                }
                sawMutableUv |= state?.ProgramSource is { } program && !program.Data.AsSpan().SequenceEqual(resources.ProgramSource.Data.AsSpan());
                sawTwoBanks |= state?.Nodes.Banks.All(bank => bank.InitializationFlag != 0 && bank.Data is not null) == true;
                sawMusicZero |= Tasks(afterEffect, 0x733690).Any(task => task.ReadByte(0x29) == 2) && current.NativeGlobalSoundProcess!.MusicPlayback!.ReadGain(0) == 0;
                stagePeak = Math.Max(stagePeak, current.NativeStage!.Groups.Max(group => group.ColorDepth));
                if (Tasks(afterEffect, 0x733CE0).Any(task => task.ReadByte(0x29) == 1))
                {
                    for (int actor = 3; actor < 7; actor++)
                    {
                        if ((graphics.Models[actor].Flags0 & 2) == 0)
                        {
                            continue;
                        }
                        Assert.Equal(graphics.Models[actor].Flags0 & 4, current.NativeGraphics.Models[actor].Flags0 & 4);
                        Assert.Equal(0x800, current.NativeGraphics.Models[actor].Flags0 & 0x800);
                    }
                    sawEnemyRestoreWithOverride = true;
                }
                stageCleared |= Tasks(afterEffect, 0x733570).Any();
                if (stageCleared)
                {
                    Assert.Equal(source.NativeStage!.Groups.Select(group => group.ColorFlags & 0xFF000000u).ToArray(), current.NativeStage.Groups.Select(group => group.ColorFlags).ToArray());
                }
                NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                sawPrivate |= frame.LastCommands.Any(item => item.Command.Packet.Bank == "20DFAB8");
                if (state?.Nodes.Work.PassGeometryCount > 0)
                {
                    Assert.Equal(afterEffect!.DrawArenaOffset, state.Nodes.Work.PrivateDrawCursor);
                    sawCommonNodeSprites |= frame.LastCommands.Any(item => item.Command.Packet.Bank is "off_B6D070" or "off_B6D094" && item.Command.Opcode is 0x2C or 0x2E);
                }
                if (directorPacket is null && Tasks(afterEffect, 0x7327B0).Any() && FindDirectorPhysicalPacket(current) is { } physical)
                {
                    directorPacket = physical;
                    directorPacketCallback = current.NativeGraphics.Callback;
                    Assert.DoesNotContain(frame.LastCommands, item => item.Command.Packet == physical && item.Command.Opcode == 0xE1);
                }
                if (cold is not null)
                {
                    cold.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold.Capture()));
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output, name! + "-after", current, pixels);
                    boundaries.Add(new(name!, prior.NativeGraphics.Callback, current.NativeGraphics.Callback, Convert.ToHexString(SHA256.HashData(pixels))));
                    requested.Remove(name!);
                }
            }
            Assert.True(initialized);
            Assert.Empty(requested);
            Assert.Equal(8, boundaries.Count);
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.True(capturedGeometry > 0);
            Assert.Equal(new short[] { 438, 439, 440, 441 }, fileIds.ToArray());
            Assert.Equal(4, fileCompletions);
            Assert.All(new short[] { 1, 2, 3, 4 }, mode => Assert.Contains(mode, modes));
            Assert.Equal(new byte[] { 0, 1, 2 }, modelPoses.Order().ToArray());
            Assert.All(new byte[] { 1, 2, 10, 12, 13, 14 }, phase => Assert.Contains(phase, cameraPhases));
            Assert.True(cameras.Count > 2);
            Assert.True(sawMutableUv && sawTwoBanks && sawCommonNodeSprites);
            Assert.False(sawPrivate);
            Assert.NotNull(directorPacket);
            Assert.True(stageCleared && sawMusicZero && sawVibration);
            Assert.True(sawEnemyRestoreWithOverride);
            Assert.Equal(4096, stagePeak);
            Assert.NotNull(vibrationHandle);
            Assert.False(battle.Capture().NativeVibrationProcess!.IsPlaying(vibrationHandle.GetValueOrDefault()));
            Assert.Equal(127, battle.Capture().NativeGlobalSoundProcess!.MusicPlayback!.ReadGain(0));
            Assert.Equal((ushort)107, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
            Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            Assert.Equal(source.NativeStage!.Groups.Select(group => (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2))).ToArray(), battle.NativeStage.Groups.Select(group => group.Flags).ToArray());
            for (int actor = 3; actor < 7; actor++)
            {
                if ((graphics.Models[actor].Flags0 & 2) == 0)
                {
                    continue;
                }
                NativeBattleGraphicsModel model = battle.NativeGraphics!.Models[actor];
                Assert.Equal(graphics.Models[actor].Flags0 & 4, model.Flags0 & 4);
                Assert.Equal(0, model.Flags0 & 0x7800);
                Assert.Equal(transition & 0xFFFFFFu, model.Color40);
                Assert.Equal((byte)0, model.Alpha7);
                Assert.Equal(graphics.Models[actor].Color44, model.Color44);
            }
            NativeBattleOriginalEffectState finished = battle.NativeGraphics!.OriginalEffect!;
            Assert.Equal((ushort)96, finished.Binding.Effect);
            Assert.Equal(0x731DE0u, finished.Binding.Entry);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            uint[] sounds = [resources.SoundAppearance, resources.SoundLater];
            NativeBattleModuleSound[] emitted = battle.Events.Skip(sourceEvents).Where(item => item.NativeModuleSound is { } sound && sounds.Contains(sound.Sound)).Select(item => item.NativeModuleSound!).ToArray();
            Assert.Equal(sounds, emitted.Select(item => item.Sound).ToArray());
            Assert.Equal(new uint[] { 0, 1 }, emitted.Select(item => item.Flags).ToArray());
            Assert.All(emitted, sound =>
            {
                Assert.Equal(128u, sound.RawPan ?? (uint?)sound.Pan);
                Assert.Equal(127u, sound.Volume);
                Assert.NotNull(sound.Allocation);
            });
            Assert.Equal(2, heard.Count(sound => sounds.Contains(sound.Sound)));
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
            BattleSession restored = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            restored.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224).ToArray());
            audio.Update(restored);
            Assert.Equal(2, heard.Count(sound => sounds.Contains(sound.Sound)));
            Assert.False(HasAction(battle));
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
            object report = new { Status = status, FirstBlocker = blocker, Scenario = "Fresh controlled post-formula seven-HP party healing; ordinary command admission remains separate", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), Callbacks = callbacks, DiagnosticMaximumCallbacks = maximum, ContactHpObservableTransitions = contacts, Completions = completions, FileIds = fileIds, FileCompletionObservableTransitions = fileCompletions, DogModes = modes.Order().ToArray(), ModelPoses = modelPoses.Order().ToArray(), CameraPhases = cameraPhases.Order().ToArray(), CameraStates = cameras.Count, StagePeak = stagePeak, VibrationHandle = vibrationHandle, SawVibrationOwned = sawVibration, SawMusicZero = sawMusicZero, SawMutableUv = sawMutableUv, SawTwoNodeBanks = sawTwoBanks, SawEnemyRestoreWithRetainedOverride = sawEnemyRestoreWithOverride, SawPrivatePackets = sawPrivate, SawCommonNodeSprites = sawCommonNodeSprites, DirectorCommonPhysicalPacket = directorPacket, DirectorPacketCallback = directorPacketCallback, DirectorPacketObservation = "Retained COMMON proxy and12physicalbytes with zero tag length; no parsed E1 command at this reference", Boundaries = boundaries, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native differential comparison", "Hardware audibility and vibration", "Other targets/headers", "Formula and ordinary command admission" } };
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static NativeBattlePrimitiveReference? FindDirectorPhysicalPacket(BattleSnapshot snapshot)
    {
        NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory!.PrimitiveProcess!;
        NativeBattleCommonPacketMemory memory = process.CommonPacketMemory!;
        foreach (NativeBattleLogicalProxy proxy in snapshot.NativeGraphics!.PrimitiveFrame!.Pending.Proxies)
        {
            NativeBattlePrimitiveReference reference = proxy.Packet;
            NativeBattleEffectArena? arena = (reference.Bank, reference.Buffer) switch
            {
                ("off_B6D070", 0) => memory.Bank070Buffer0,
                ("off_B6D070", 1) => memory.Bank070Buffer1,
                ("off_B6D094", 0) => memory.Bank094Buffer0,
                ("off_B6D094", 1) => memory.Bank094Buffer1,
                _ => null
            };
            if (arena is null || proxy.Flags20 != 0 || proxy.ModuleDraw is not null || proxy.VertexPayload4.Any(value => value != 0) || !arena.Regions.Any(region => reference.ByteOffset >= region.Offset && (long)reference.ByteOffset + 12 <= (long)region.Offset + region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < (long)reference.ByteOffset + 12 && (long)region.Offset + region.Length > reference.ByteOffset))
            {
                continue;
            }
            byte[] bytes = (process with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, 12);
            if (bytes[3] == 0 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) == 0xE1000020 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)) == 0)
            {
                return reference;
            }
        }
        return null;
    }

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested, NativeBattleMoogle96Resources resources)
    {
        NativeBattleOriginalEffectState? effect = Selected(snapshot);
        NativeBattleMoogle96State? state = snapshot.NativeEffectMemory?.Moogle96;
        if (requested.Contains("io-pending") && state?.Io?.Busy is > 0 && snapshot.NativeFileIo!.Rows.Any(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x732590))
        {
            return "io-pending";
        }
        if (requested.Contains("mode-handoff") && state?.DogControl is { } dog && Word(dog, 0x40) == 1 && Word(dog, 0x42) == 2)
        {
            return "mode-handoff";
        }
        if (requested.Contains("camera-late") && Tasks(effect, 0x732A00).Any(task => task.ReadByte(0x29) == 10))
        {
            return "camera-late";
        }
        if (requested.Contains("program-uv-live") && Tasks(effect, 0x733FC0).Any(task => task.ReadDword(0x98) >= 2) && state?.ProgramSource is { } program && !program.Data.AsSpan().SequenceEqual(resources.ProgramSource.Data.AsSpan()))
        {
            return "program-uv-live";
        }
        NativeBattleModulePoolTask? model = Tasks(effect, 0x739610).FirstOrDefault();
        if (model is not null)
        {
            if (requested.Contains("pose0") && model.ReadByte(0x9C) == 0 && model.ReadByte(0x29) == 1)
            {
                return "pose0";
            }
            if (requested.Contains("pose1-repeat") && model.ReadByte(0x9C) == 1 && model.ReadByte(0x29) == 3)
            {
                return "pose1-repeat";
            }
            if (requested.Contains("pose2") && model.ReadByte(0x9C) == 2 && model.ReadByte(0x29) == 4)
            {
                return "pose2";
            }
        }
        return requested.Contains("nodes-second") && Tasks(effect, 0x734840).Any(task => task.ReadDword(0x30) == 0x1696DE8 && task.ReadByte(0x29) == 1) ? "nodes-second" : null;
    }

    private static short Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static IEnumerable<NativeBattleModulePoolTask> Tasks(NativeBattleOriginalEffectState? effect, uint callback) => effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0 && task.Callback == callback) ?? [];
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: not null, Binding.Effect: 96, Binding.Entry: 0x731DE0 } effect ? effect : null;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private sealed record Boundary(string Name, long BeforeCallback, long AfterCallback, string RgbaSha256);
}
