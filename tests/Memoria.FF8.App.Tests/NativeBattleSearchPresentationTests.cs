using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSearchPresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_Search_cast_completes_with_original_resources_and_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_SEARCH_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_SEARCH_SNAPSHOT to a complete queued Search party-contact fixture.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SEARCH_OUTPUT") ?? throw new InvalidDataException("A distinct MEMORIA_FF8_SEARCH_OUTPUT directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(), "Output must be a distinct empty directory.");
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_SEARCH_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 512;
        Assert.True(maximum > 0, "The explicit diagnostic callback budget must be positive.");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        NativeBattleGraphicsState graphics = source.NativeGraphics ?? throw new InvalidDataException("The fixture lacks native graphics.");
        NativeBattleActionPacketsState packets = source.NativeActionPackets ?? throw new InvalidDataException("The fixture lacks physical action packets.");
        int slot = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
        uint offset = unchecked(graphics.Descriptors.Packets[slot].Argument - 0x1D280C4u);
        Assert.True(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        int headerIndex = (int)(offset / 20);
        NativeBattleActionHeaderSlot header = packets.Headers[headerIndex];
        Assert.Equal((byte)240, header.ReadByte(1));
        Assert.Equal((ushort)14, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)92, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.True(graphics.ActionGraphics is null || graphics.ActionGraphics.HeaderIndex == headerIndex);
        NativeBattleContactPacket contact = packets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("The fixture lacks its explicit contact owner."));
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        ushort initialHp = source.NativePartyPresentation!.Rows[0]!.HitPoints;
        Assert.True(initialHp >= 7);
        ushort expectedHp = (ushort)(initialHp - 7);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([92])[91];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[91]));
        NativeBattleSearchResources resources = installed.SearchResources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        int sourceEvents = battle.Events.Count;
        HashSet<string> requested = ["model-start", "pose1", "mesh-live", "pose3", "program-live", "departure"];
        List<Boundary> boundaries = [];
        int callbacks = 0;
        int contactTransitions = 0;
        int completions = 0;
        bool initialized = false;
        bool sawTargetOverride = false;
        bool sawModelTim = false;
        BattleSnapshot prior = source;
        NativeBattleRestorationPresentationTests.Publish(output, "initial", source, null);
        try
        {
            NativeBattleRestorationPresentationTests.Publish(output, "initial-rendered", source, scene.Render(battle, 320, 224));
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The explicit Search diagnostic callback budget was exceeded; it is not a native effect duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? beforeEffect = Selected(prior);
                string? name = BoundaryName(beforeEffect, requested);
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
                }
                ushort previousHp = prior.NativePartyPresentation!.Rows[0]!.HitPoints;
                ushort hp = current.NativePartyPresentation!.Rows[0]!.HitPoints;
                if (previousHp != hp)
                {
                    Assert.Equal(initialHp, previousHp);
                    Assert.Equal(expectedHp, hp);
                    contactTransitions++;
                }
                Assert.True(hp == initialHp || hp == expectedHp);
                sawTargetOverride |= current.NativeGraphics.Models[0].ActiveStagedResourceRow100 is not null;
                sawModelTim |= current.NativeEffectMemory?.Textures?.Slots.Any(item => item?.Source?.Identity == resources.ModelProfile.ModelTim.Identity) == true;
                if (cold is not null)
                {
                    cold.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold.Capture()));
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                    NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                    int privatePackets = frame.LastCommands.Count(item => item.Command.Packet.Bank == "20DFAB8");
                    NativeBattlePrimitiveFrame without = frame with { LastCommands = frame.LastCommands.Where(item => item.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    byte[] background = scene.Render(plain, 320, 224).ToArray();
                    int changed = Enumerable.Range(0, pixels.Length / 4).Count(pixel => !pixels.AsSpan(pixel * 4, 3).SequenceEqual(background.AsSpan(pixel * 4, 3)));
                    NativeBattleRestorationPresentationTests.Publish(output, name! + "-after", current, pixels);
                    boundaries.Add(new(name!, prior.NativeGraphics.Callback, current.NativeGraphics.Callback, privatePackets, changed, Convert.ToHexString(SHA256.HashData(pixels))));
                    requested.Remove(name!);
                }
            }
            Assert.True(initialized);
            Assert.Empty(requested);
            Assert.Equal(6, boundaries.Count);
            Assert.Contains(boundaries, item => item.PrivatePackets > 0 && item.ChangedRgbPixels > 0);
            Assert.Equal(1, contactTransitions);
            Assert.Equal(1, completions);
            Assert.True(sawTargetOverride);
            Assert.True(sawModelTim);
            Assert.Equal(expectedHp, battle.Capture().NativePartyPresentation!.Rows[0]!.HitPoints);
            NativeBattleOriginalEffectState finished = Assert.IsType<NativeBattleOriginalEffectState>(battle.NativeGraphics!.OriginalEffect);
            Assert.Equal((ushort)92, finished.Binding.Effect);
            Assert.Equal(0x755130u, finished.Binding.Entry);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            uint[] ids = [resources.SoundAppearance, resources.VoiceAppearance, resources.SoundPose1, resources.SoundProgram, resources.VoiceDeparture];
            NativeBattleModuleSound[] emitted = battle.Events.Skip(sourceEvents).Where(item => item.NativeModuleSound is { } sound && ids.Contains(sound.Sound)).Select(item => item.NativeModuleSound!).ToArray();
            Assert.Equal(ids, emitted.Select(item => item.Sound).ToArray());
            Assert.Equal(new uint[] { 0, 0, 1, 1, 0 }, emitted.Select(item => item.Flags).ToArray());
            Assert.All(emitted, sound =>
            {
                Assert.Equal(128u, sound.RawPan ?? (uint?)sound.Pan);
                Assert.Equal(127u, sound.Volume);
                Assert.NotNull(sound.Allocation);
            });
            Assert.Equal(5, heard.Count(sound => ids.Contains(sound.Sound)));
            BattleSnapshot completed = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, "completed", completed, scene.Render(battle, 320, 224));
            BattleSession restored = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            restored.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224).ToArray());
            audio.Update(restored);
            Assert.Equal(5, heard.Count(sound => ids.Contains(sound.Sound)));
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
            object report = new { Status = status, FirstBlocker = blocker, SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), Callbacks = callbacks, DiagnosticMaximumCallbacks = maximum, ContactObservableTransitions = contactTransitions, Completions = completions, SawTargetOverride = sawTargetOverride, SawModelTim = sawModelTim, Boundaries = boundaries, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native differential comparison", "Hardware audibility", "Other targets/headers", "Formula and ordinary command admission" } };
            using FileStream stream = new(Path.Combine(output, "result.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(stream, report, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static string? BoundaryName(NativeBattleOriginalEffectState? effect, HashSet<string> requested)
    {
        NativeBattleModulePoolTask? model = effect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSearchController.ModelDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x7557B0);
        int? stage = model?.ReadByte(0x29);
        if (requested.Contains("model-start") && stage == 1)
        {
            return "model-start";
        }
        if (requested.Contains("pose1") && stage == 2 && model!.ReadWord(0x136) == 6)
        {
            return "pose1";
        }
        if (requested.Contains("mesh-live") && effect?.Pools.Any(pool => pool.Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x755EE0)) == true)
        {
            return "mesh-live";
        }
        if (requested.Contains("pose3") && stage == 4 && model!.ReadWord(0x136) == 11)
        {
            return "pose3";
        }
        if (requested.Contains("program-live") && effect?.Pools.Any(pool => pool.Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x758CB0 && task.ReadDword(0x98) == 1)) == true)
        {
            return "program-live";
        }
        if (requested.Contains("departure") && stage == 5)
        {
            return "departure";
        }
        return null;
    }

    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: not null, Binding.Effect: 92, Binding.Entry: 0x755130 } effect ? effect : null;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private sealed record Boundary(string Name, long BeforeCallback, long AfterCallback, int PrivatePackets, int ChangedRgbPixels, string RgbaSha256);
}
