using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleThundaga105PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_Thundaga_runs_original_capture_fragments_and_actual_common_io_with_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_THUNDAGA105_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_THUNDAGA105_SNAPSHOT to a complete queued controlled seven-HP Thundaga component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_THUNDAGA105_OUTPUT") ?? throw new InvalidDataException("A distinct output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_THUNDAGA105_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 1024;
        Assert.True(maximum > 0);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction kernel = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.ActionId == 9 && action.MagicId == 105);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        NativeBattleGraphicsState initial = source.NativeGraphics!;
        int descriptor = Assert.Single(initial.Descriptors.Queue, slot => initial.Descriptors.Packets[slot].Command == 104);
        uint delta = unchecked(initial.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
        Assert.True(delta % 20 == 0 && delta / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        int headerIndex = checked((int)(delta / 20));
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[headerIndex];
        Assert.Equal(kernel.Command, header.ReadByte(1));
        Assert.Equal(kernel.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)105, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.True(initial.ActionGraphics is null || initial.ActionGraphics.HeaderIndex == headerIndex);
        int contactIndex = header.FirstContactIndex ?? throw new InvalidDataException("A physical contact owner is required.");
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(contactIndex);
        int target = contact.Primary.ActorIndex;
        Assert.InRange(target, 0, 2);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        ushort initialHp = source.NativePartyPresentation!.Rows[target]!.HitPoints;
        Assert.True(initialHp >= 7);
        ushort finalHp = (ushort)(initialHp - 7);
        uint? initialColor = initial.Models[target].Color40;
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([105])[104];
        Assert.Equal(0x6D3DB0u, installed.Entry);
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[104]));
        NativeBattleThundaga105Resources resources = installed.Thundaga105Resources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        int stops = 0;
        List<NativeBattleMusicCommandReceipt> music = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add, stopSounds: () => stops++, playMusicCommand: music.Add);
        audio.Update(battle);
        heard.Clear();
        int initialEvents = battle.Events.Count;
        HashSet<string> requested = ["primary-1", "primary-3", "primary-28", "capture-after", "moving-live", "stationary-capacity", "stationary-live", "contact-before", "common-io"];
        List<Boundary> boundaries = [];
        int callbacks = 0;
        int completions = 0;
        int contacts = 0;
        int debrisPeak = 0;
        int fragmentFaces = 0;
        int movingPeak = 0;
        int customWindows = 0;
        int privatePackets = 0;
        int captureChangedPixels = 0;
        int captureNonzeroPixels = 0;
        bool initialized = false;
        bool commonIo = false;
        bool capacityFailure = false;
        bool captureStatics = false;
        bool captureOrder = false;
        NativeCrtRandomState? controllerStart = null;
        NativeCrtRandomState? controllerEnd = null;
        int? controllerDraws = null;
        BattleSnapshot prior = source;
        NativeBattleRestorationPresentationTests.Publish(output, "initial", source, scene.Render(battle, 320, 224));
        try
        {
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The diagnostic callback limit was exceeded; it is not a native duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? beforeEffect = Selected(prior);
                if (controllerStart is null && beforeEffect?.ReturnedDescriptor is > 0)
                {
                    controllerStart = prior.NativeCrtRandom;
                }
                string? name = BoundaryName(prior, requested);
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
                Assert.Equal(prior.NativeGraphics!.Callback + 1, current.NativeGraphics!.Callback);
                NativeBattleOriginalEffectState? afterEffect = Selected(current);
                initialized |= afterEffect?.ReturnedDescriptor is > 0;
                if (beforeEffect?.ReturnedDescriptor is > 0 && afterEffect?.ReturnedDescriptor == 0)
                {
                    completions++;
                    controllerEnd = current.NativeCrtRandom;
                }
                ushort previousHp = prior.NativePartyPresentation!.Rows[target]!.HitPoints;
                ushort hp = current.NativePartyPresentation!.Rows[target]!.HitPoints;
                if (hp != previousHp)
                {
                    Assert.Equal(initialHp, previousHp);
                    Assert.Equal(finalHp, hp);
                    contacts++;
                }
                Assert.True(hp == initialHp || hp == finalHp);
                debrisPeak = Math.Max(debrisPeak, DebrisCount(current));
                movingPeak = Math.Max(movingPeak, Tasks(afterEffect, 0x6D6100).Count());
                if (Primary(beforeEffect)?.ReadWord(0xC) == 45)
                {
                    Assert.Equal(32, Tasks(beforeEffect, 0x6D6100).Count());
                    Assert.Empty(Tasks(afterEffect, 0x6D61C0));
                    capacityFailure = true;
                }
                if (beforeEffect?.Pools.Length == 4 && afterEffect?.Pools.Length == 4)
                {
                    for (int index = 0; index < beforeEffect.Pools[2].Slots.Length; index++)
                    {
                        NativeBattleModulePoolTask previous = beforeEffect.Pools[2].Slots[index];
                        NativeBattleModulePoolTask next = afterEffect.Pools[2].Slots[index];
                        if ((previous.Flags & 1) != 0 && (next.Flags & 1) == 0)
                        {
                            NativeBattleStaticMeshResource mesh = current.NativeEffectMemory!.Thundaga105!.MutableFragments.Single(mesh => mesh.Address == previous.ReadDword(0x38));
                            fragmentFaces += NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), 2).Count + NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), 6).Count;
                        }
                    }
                }
                NativeBattlePrimitiveFrame? primitive = current.NativeGraphics.PrimitiveFrame;
                if (primitive is not null)
                {
                    privatePackets += primitive.LastCommands.Count(command => command.Command.Packet.Bank == "20DFAB8");
                    customWindows += primitive.LastCommands.Count(command => command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0xE2);
                    var staticCommands = primitive.LastCommands.Where(command => command.Pass == 0 && command.Command.Packet.Bank == NativeBattleThundaga105CaptureMemory.PacketBank).Select(command => command.Command.Opcode).ToArray();
                    if (staticCommands.Length > 0)
                    {
                        captureOrder |= staticCommands.Length >= 3 && staticCommands[0] == 0xE5 && staticCommands[1] == 0xE3 && staticCommands[2] == 0xE4;
                    }
                }
                if (Primary(beforeEffect)?.ReadWord(0xC) == 28)
                {
                    NativeBattleThundaga105CaptureMemory memory = current.NativeEffectMemory!.Thundaga105Capture!;
                    Assert.Equal(0xE30402C0u, Dword(memory.Read(0x64, 4)));
                    Assert.Equal(0xE405FF3Fu, Dword(memory.Read(0x68, 4)));
                    Assert.Equal(0xE50802C0u, Dword(memory.Read(0x4C, 4)));
                    captureStatics = true;
                    byte[] beforeVram = prior.NativeEffectMemory!.Textures!.ReadVram();
                    byte[] afterVram = current.NativeEffectMemory.Textures!.ReadVram();
                    for (int y = 256; y < 384; y++)
                    {
                        for (int x = 704; x < 832; x++)
                        {
                            int offset = (y * 1024 + x) * 2;
                            ushort beforePixel = BinaryPrimitives.ReadUInt16LittleEndian(beforeVram.AsSpan(offset));
                            ushort afterPixel = BinaryPrimitives.ReadUInt16LittleEndian(afterVram.AsSpan(offset));
                            captureChangedPixels += beforePixel != afterPixel ? 1 : 0;
                            captureNonzeroPixels += afterPixel != 0 ? 1 : 0;
                        }
                    }
                }
                commonIo |= CommonIo(current);
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
            Assert.Equal(9, boundaries.Count);
            Assert.Equal(1, completions);
            Assert.Equal(1, contacts);
            Assert.Equal(finalHp, battle.Capture().NativePartyPresentation!.Rows[target]!.HitPoints);
            Assert.Equal(initialColor, battle.NativeGraphics!.Models[target].Color40);
            Assert.Equal(310, fragmentFaces);
            Assert.True(debrisPeak > 0);
            Assert.Equal(32, movingPeak);
            Assert.True(capacityFailure);
            Assert.True(privatePackets > 0 && customWindows > 0);
            Assert.True(captureStatics && captureOrder);
            Assert.True(captureChangedPixels > 0 && captureNonzeroPixels > 0);
            Assert.True(commonIo);
            Assert.NotNull(controllerStart);
            Assert.NotNull(controllerEnd);
            NativeCrtRandomState expected = controllerStart!;
            for (int draw = 0; draw < 1375; draw++)
            {
                expected = expected.Draw(out _);
            }
            Assert.Equal(expected, controllerEnd);
            controllerDraws = 1375;
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.Equal(4, finished.Pools.Length);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            NativeBattleModuleSound sound = Assert.Single(battle.Events.Skip(initialEvents), item => item.NativeModuleSound?.Sound == resources.Sound).NativeModuleSound!;
            Assert.Equal(0x101u, sound.Flags);
            Assert.NotNull(sound.Allocation);
            Assert.Single(heard, sound => sound.Sound == resources.Sound);
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
            BattleSession completed = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            completed.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(completed.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(completed, 320, 224).ToArray());
            audio.Update(completed);
            Assert.Single(heard, sound => sound.Sound == resources.Sound);
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
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Scenario = "Queued declared controlled seven-HP component; command2/action9/effect105; ordinary producer/formula admission remains separate", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), Callbacks = callbacks, DiagnosticMaximumCallbacks = maximum, Completions = completions, ContactHpTransitions = contacts, CompletedFragmentFaces = fragmentFaces, DebrisPeak = debrisPeak, MovingSpritePeak = movingPeak, SawF45CapacityFailure = capacityFailure, PrivateParsedCommands = privatePackets, CustomE2Commands = customWindows, SawCaptureStatics = captureStatics, SawCaptureOrder = captureOrder, CaptureChangedPixels = captureChangedPixels, CaptureNonzeroPixels = captureNonzeroPixels, ControllerCrtStart = controllerStart, ControllerCrtEnd = controllerEnd, ControllerCrtDraws = controllerDraws, SawActualCommonIo = commonIo, SoundStopRequests = stops, MusicCommands = music, Boundaries = boundaries, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry original native visual differential", "Ordinary command producer and formula admission", "Other targets/headers", "Hardware audibility" } }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested)
    {
        NativeBattleOriginalEffectState? effect = Selected(snapshot);
        int? phase = Primary(effect)?.ReadWord(0xC);
        string? name = phase switch { 1 => "primary-1", 3 => "primary-3", 28 => "primary-28", 29 => "capture-after", 35 => "moving-live", 46 => "stationary-capacity", 56 => "stationary-live", 64 => "contact-before", _ => null };
        if (name is not null && requested.Contains(name))
        {
            return name;
        }
        return requested.Contains("common-io") && CommonIo(snapshot) ? "common-io" : null;
    }

    private static int DebrisCount(BattleSnapshot snapshot)
    {
        if (Selected(snapshot)?.Pools.Length != 4)
        {
            return 0;
        }
        byte[] bank = snapshot.NativeEffectMemory!.Arena!.Read(0x2A24, 310 * 0x50);
        return Enumerable.Range(0, 310).Count(index => BinaryPrimitives.ReadInt16LittleEndian(bank.AsSpan(index * 0x50 + 6)) != 0);
    }

    private static uint Dword(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    private static bool CommonIo(BattleSnapshot snapshot) => snapshot.NativeFileIo?.Rows.Any(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470) == true;
    private static NativeBattleModulePoolTask? Primary(NativeBattleOriginalEffectState? effect) => Tasks(effect, 0x6D43B0).FirstOrDefault();
    private static IEnumerable<NativeBattleModulePoolTask> Tasks(NativeBattleOriginalEffectState? effect, uint callback) => effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0 && task.Callback == callback) ?? [];
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 105, Binding.Entry: 0x6D3DB0 } effect ? effect : null;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private sealed record Boundary(string Name, long BeforeCallback, long AfterCallback, string RgbaSha256);
}
