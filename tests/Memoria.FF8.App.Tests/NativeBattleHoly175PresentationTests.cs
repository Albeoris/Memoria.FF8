using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleHoly175PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_Holy_runs_capture_warp_all_children_contact_and_actual_completion_with_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOLY175_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_HOLY175_SNAPSHOT to a complete controlled seven-HP queued Holy component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOLY175_OUTPUT") ?? throw new InvalidDataException("A distinct output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOLY175_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 1024;
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 2 && item.ActionId == 14 && item.MagicId == 175);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        int descriptor = Assert.Single(graphics.Descriptors.Queue, slot => graphics.Descriptors.Packets[slot].Command == 104);
        uint delta = unchecked(graphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
        Assert.True(delta % 20 == 0 && delta / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(delta / 20))];
        Assert.Equal(action.Command, header.ReadByte(1));
        Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)175, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        int target = contact.Primary.ActorIndex;
        Assert.InRange(target, 0, 2);
        ushort initialHp = source.NativePartyPresentation!.Rows[target]!.HitPoints;
        ushort finalHp = checked((ushort)(initialHp - 7));
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([175])[174];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[174]));
        NativeBattleHoly175Resources resources = installed.Holy175Resources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add, stopSounds: () => { }, playMusicCommand: _ => { });
        audio.Update(battle);
        heard.Clear();
        int initialEvents = battle.Events.Count;
        HashSet<string> requested = ["grid-before", "faces-before", "warp-first", "tim-before", "ray-spawn", "spiral-spawn", "stage-restore", "contact-before", "common-io"];
        List<Boundary> boundaries = [];
        int callbacks = 0;
        int completions = 0;
        int contacts = 0;
        int captureFrames = 0;
        int sprite31Peak = 0;
        int sprite4Peak = 0;
        int compoundWindows = 0;
        int preparedCapturePages = 0;
        bool commonIo = false;
        NativeCrtRandomState? start = null;
        NativeCrtRandomState? end = null;
        BattleSnapshot prior = source;
        NativeBattleRestorationPresentationTests.Publish(output, "initial", source, scene.Render(battle, 320, 224));
        try
        {
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The diagnostic limit is not a native duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                if (start is null && before?.ReturnedDescriptor is > 0)
                {
                    start = prior.NativeCrtRandom;
                }
                string? name = BoundaryName(prior, requested);
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
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                    end = current.NativeCrtRandom;
                }
                ushort previousHp = prior.NativePartyPresentation!.Rows[target]!.HitPoints;
                ushort hp = current.NativePartyPresentation!.Rows[target]!.HitPoints;
                if (hp != previousHp)
                {
                    Assert.Equal(initialHp, previousHp);
                    Assert.Equal(finalHp, hp);
                    contacts++;
                }
                NativeBattleHoly175State state = current.NativeEffectMemory!.Holy175!;
                if (after?.Pools.Length == 2)
                {
                    sprite31Peak = Math.Max(sprite31Peak, Active(state.Sprite31Rows, 100));
                    sprite4Peak = Math.Max(sprite4Peak, Active(state.Sprite4Rows, 80));
                }
                NativeBattlePrimitiveFrame? primitive = current.NativeGraphics.PrimitiveFrame;
                if (primitive is not null)
                {
                    captureFrames += primitive.LastCommands.Any(command => command.Pass == 1 && command.Command.Packet.Bank == NativeBattleHoly175CaptureMemory.PacketBank && command.Command.Opcode == 0x28) ? 1 : 0;
                    compoundWindows += primitive.LastCommands.Count(command => command.Command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.Command.WordOffset == 1 && command.Command.Opcode == 0xE2);
                }
                if (Selected(prior)?.Pools.SelectMany(pool => pool.Slots).Any(task => (task.Flags & 1) != 0 && task.Callback == 0x5D1BD0 && task.ReadWord(0xC) == 5) == true)
                {
                    NativeBattlePrimitiveFrame captured = current.NativeGraphics.PrimitiveFrame!;
                    var windows = captured.LastCommands.Where(command => command.Pass > 0 && command.Command.Packet.Bank == NativeBattleHoly175WarpDrawing.PacketBank && command.DrawMode.TexturePixelSnapshot is not null).Select(command => command.DrawMode).DistinctBy(mode => mode.TexturePixelSnapshot);
                    foreach (NativeBattlePrimitiveDrawMode mode in windows)
                    {
                        NativeBattlePcTextureWindowSelection selection = mode.TextureSelection!;
                        NativeBattlePcTextureWindowPixels saved = captured.LastTextureSnapshots!.Value[mode.TexturePixelSnapshot!.Value];
                        NativeBattlePcTextureWindowPixels actual = NativeBattlePcTextureWindowPixels.Capture(selection.Row, new(selection.X, selection.Y, selection.Width, selection.Height), current.NativeEffectMemory.Textures!);
                        Assert.Equal(actual.Data.ToArray(), saved.Data.ToArray());
                        preparedCapturePages++;
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
                    boundaries.Add(new(name!, current.NativeGraphics.Callback, Convert.ToHexString(SHA256.HashData(rgba))));
                    requested.Remove(name!);
                }
            }
            Assert.Empty(requested);
            Assert.Equal(9, boundaries.Count);
            Assert.Equal(1, completions);
            Assert.Equal(1, contacts);
            Assert.Equal(10, captureFrames);
            Assert.True(sprite31Peak > 0 && sprite4Peak > 0 && compoundWindows > 0);
            Assert.True(commonIo);
            Assert.Equal(2, preparedCapturePages);
            Assert.NotNull(start);
            Assert.NotNull(end);
            NativeCrtRandomState expected = start!;
            for (int index = 0; index < 7131; index++)
            {
                expected = expected.Draw(out _);
            }
            Assert.Equal(expected, end);
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.Equal(2, finished.Pools.Length);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.Equal(0, Active(battle.NativeEffectMemory.Holy175!.Sprite31Rows, 100));
            Assert.Equal(0, Active(battle.NativeEffectMemory.Holy175.Sprite4Rows, 80));
            Assert.All(battle.NativeStage!.Groups, group =>
            {
                Assert.Equal((short)0, group.ColorDepth);
                Assert.True((group.Flags & 2) != 0);
            });
            NativeBattleModuleSound sound = Assert.Single(battle.Events.Skip(initialEvents), item => item.NativeModuleSound?.Sound == resources.Sound).NativeModuleSound!;
            Assert.Equal(1u, sound.Flags);
            Assert.NotNull(sound.Allocation);
            Assert.Single(heard, item => item.Sound == resources.Sound);
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
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Scenario = "Controlled queued command2/action14/effect175 sevenHP postformula component; ordinaryproducer/formula separate", Callbacks = callbacks, Completions = completions, ContactHpTransitions = contacts, CaptureFrames = captureFrames, Sprite31Peak = sprite31Peak, Sprite4Peak = sprite4Peak, CompoundWindowCommands = compoundWindows, PreparedCapturePages = preparedCapturePages, ControllerCrtStart = start, ControllerCrtEnd = end, ExpectedDerivedControllerCrtDraws = 7131, SawActualCommonIo = commonIo, Boundaries = boundaries, SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native visual comparison", "Ordinary producer/formula", "Other targets/headers", "Hardware audibility" } }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static int Active(NativeBattleEffectArena rows, int count) => Enumerable.Range(0, count).Count(index => (rows.Read(index * 24, 1)[0] & 1) != 0);
    private static bool CommonIo(BattleSnapshot snapshot) => snapshot.NativeFileIo?.Rows.Any(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470) == true;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 175, Binding.Entry: 0x5D1AD0 } effect ? effect : null;
    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested)
    {
        NativeBattleModulePoolTask? primary = Selected(snapshot)?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x5D1BD0);
        string? name = primary?.ReadWord(0xC) switch { 3 => "grid-before", 4 => "faces-before", 5 => "warp-first", 30 => "tim-before", 66 => "ray-spawn", 78 => "spiral-spawn", 103 => "stage-restore", 106 => "contact-before", _ => null };
        if (name is not null && requested.Contains(name))
        {
            return name;
        }
        return requested.Contains("common-io") && CommonIo(snapshot) ? "common-io" : null;
    }
    private sealed record Boundary(string Name, long Callback, string RgbaSha256);
}
