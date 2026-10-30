using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRecover93PresentationTests
{
    private static readonly TimeSpan Callback = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1) + TimeSpan.FromTicks(1);

    [Fact]
    public void External_Recover_cast_completes_with_original_resources_and_independent_live_restores()
    {
        RunExternal(93);
    }

    internal static void RunExternal(ushort effectId)
    {
        NativeBattleAngeloRestorationProfile profile = effectId switch { 93 => NativeBattleAngeloRestorationProfile.Recover93, 94 => NativeBattleAngeloRestorationProfile.Reverse94, _ => throw new NotSupportedException("The external restoration gate requires its proved physical profile.") };
        bool revival = effectId == 94;
        int targetIndex = revival ? 1 : 0;
        string prefix = revival ? "MEMORIA_FF8_REVERSE94_" : "MEMORIA_FF8_RECOVER93_";
        string? sourcePath = Environment.GetEnvironmentVariable(prefix + "SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set " + prefix + "SNAPSHOT to a complete queued controlled post-formula restoration fixture.");
        string output = Environment.GetEnvironmentVariable(prefix + "OUTPUT") ?? throw new InvalidDataException("A distinct " + prefix + "OUTPUT directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(), "Output must be a distinct empty directory.");
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable(prefix + "MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 512;
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
        Assert.Equal((ushort)(revival ? 13 : 12), NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal(effectId, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.True(graphics.ActionGraphics is null || graphics.ActionGraphics.HeaderIndex == headerIndex);
        NativeBattleContactPacket contact = packets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("The fixture lacks its explicit contact owner."));
        Assert.Equal((byte)targetIndex, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)1, (byte)(contact.Primary.HitFlags3 & 1));
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((byte)4, graphics.Models[0].ModelId4);
        ushort initialHp = source.NativePartyPresentation!.Rows[targetIndex]!.HitPoints;
        Assert.Equal((ushort)(revival ? 0 : 100), initialHp);
        ushort expectedHp = (ushort)(initialHp + 7);
        if (revival)
        {
            BattleCombatantSnapshot target = source.Combatants.Single(actor => actor.NativeParticipant?.ActorIndex == targetIndex);
            NativeBattleStatusSnapshot status = target.NativeStatus!;
            Assert.Equal(7, target.CurrentHp);
            Assert.Equal(0, status.Saved & 1);
            Assert.Equal((ushort)(status.Saved | 1), status.Presentation.Saved);
            Assert.Equal(0, contact.Primary.SavedStatuses4 & 1);
            Assert.Equal((ushort)100, source.NativePartyPresentation.Rows[0]!.HitPoints);
        }
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[effectId - 1]));
        NativeBattleRecover93Resources resources = (revival ? installed.Reverse94Resources : installed.Recover93Resources)!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        int sourceEvents = battle.Events.Count;
        HashSet<string> requested = ["model-start", "program-live", "fade-35", "fade-41", "fade-70", "fade-76"];
        List<Boundary> boundaries = [];
        int callbacks = 0;
        int capturedGeometry = 0;
        int contactTransitions = 0;
        int completions = 0;
        bool initialized = false;
        bool sawModelTim = false;
        bool stageCleared = false;
        int stagePeak = 0;
        List<short> stageDepths = [];
        uint[]? stageHighBytes = null;
        BattleSnapshot prior = source;
        NativeBattleRestorationPresentationTests.Publish(output, "initial", source, null);
        try
        {
            NativeBattleRestorationPresentationTests.Publish(output, "initial-rendered", source, scene.Render(battle, 320, 224));
            while (HasAction(battle))
            {
                Assert.True(callbacks < maximum, "The explicit restoration diagnostic callback budget was exceeded; it is not a native effect duration.");
                prior = battle.Capture();
                NativeBattleOriginalEffectState? beforeEffect = Selected(prior, profile);
                int? fadeCounter = Live(beforeEffect, profile.Code(0x754E60))?.ReadWord(0x24);
                if (fadeCounter == 35)
                {
                    stageHighBytes = prior.NativeStage!.Groups.Select(group => group.ColorFlags & 0xFF000000u).ToArray();
                }
                string? name = BoundaryName(beforeEffect, requested, profile);
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
                capturedGeometry += NativeBattleRestorationSummonCaptureAssertions.AssertOwned(current, effectId);
                Assert.Equal(prior.NativeGraphics!.Callback + 1, current.NativeGraphics!.Callback);
                NativeBattleOriginalEffectState? afterEffect = Selected(current, profile);
                initialized |= afterEffect?.ReturnedDescriptor is > 0;
                if (beforeEffect?.ReturnedDescriptor is > 0 && afterEffect?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                ushort previousHp = prior.NativePartyPresentation!.Rows[targetIndex]!.HitPoints;
                ushort hp = current.NativePartyPresentation!.Rows[targetIndex]!.HitPoints;
                if (previousHp != hp)
                {
                    Assert.Equal(initialHp, previousHp);
                    Assert.Equal(expectedHp, hp);
                    contactTransitions++;
                }
                Assert.True(hp == initialHp || hp == expectedHp);
                Assert.Equal(graphics.Models[targetIndex].ActiveStagedResourceRow100, current.NativeGraphics.Models[targetIndex].ActiveStagedResourceRow100);
                if (revival)
                {
                    Assert.Equal((ushort)100, current.NativePartyPresentation.Rows[0]!.HitPoints);
                }
                sawModelTim |= current.NativeEffectMemory?.Textures?.Slots.Any(item => item?.Source?.Identity == resources.ModelTim.Identity) == true;
                if (fadeCounter is >= 35 and <= 76)
                {
                    int expectedDepth = fadeCounter <= 41 ? Math.Min(1536, (fadeCounter.Value - 35) * 256) : fadeCounter <= 70 ? 1536 : Math.Max(0, 1536 - (fadeCounter.Value - 70) * 256);
                    Assert.All(current.NativeStage!.Groups, group => Assert.Equal((short)expectedDepth, group.ColorDepth));
                    stageCleared = true;
                }
                if (stageCleared)
                {
                    Assert.Equal(stageHighBytes!, current.NativeStage!.Groups.Select(group => group.ColorFlags).ToArray());
                    stagePeak = Math.Max(stagePeak, current.NativeStage.Groups.Max(group => group.ColorDepth));
                    short depth = current.NativeStage.Groups[0].ColorDepth;
                    if (stageDepths.Count == 0 || stageDepths[^1] != depth)
                    {
                        stageDepths.Add(depth);
                    }
                }
                if (cold is not null)
                {
                    cold.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold.Capture()));
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                    NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                    int privatePackets = frame.LastCommands.Count(item => item.Command.Packet.Bank == "20DFAB8");
                    int commonFramePackets = frame.LastCommands.Count(item => item.Command.Packet.Bank != "20DFAB8");
                    NativeBattlePrimitiveFrame without = frame with { LastCommands = frame.LastCommands.Where(item => item.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    byte[] background = scene.Render(plain, 320, 224).ToArray();
                    int changed = Enumerable.Range(0, pixels.Length / 4).Count(pixel => !pixels.AsSpan(pixel * 4, 3).SequenceEqual(background.AsSpan(pixel * 4, 3)));
                    NativeBattleRestorationPresentationTests.Publish(output, name! + "-after", current, pixels);
                    boundaries.Add(new(name!, prior.NativeGraphics.Callback, current.NativeGraphics.Callback, privatePackets, commonFramePackets, changed, Convert.ToHexString(SHA256.HashData(pixels))));
                    requested.Remove(name!);
                }
            }
            Assert.True(initialized);
            Assert.Empty(requested);
            Assert.Equal(6, boundaries.Count);
            Assert.Contains(boundaries, item => item.PrivatePackets > 0 && item.ChangedRgbPixels > 0);
            Assert.Contains(boundaries, item => item.CommonFramePackets > 0);
            Assert.Equal(1, contactTransitions);
            Assert.Equal(1, completions);
            Assert.True(capturedGeometry > 0);
            Assert.True(sawModelTim);
            Assert.True(stageCleared);
            Assert.Equal(1536, stagePeak);
            Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            Assert.Equal(stageHighBytes!, battle.NativeStage.Groups.Select(group => group.ColorFlags).ToArray());
            Assert.Equal(expectedHp, battle.Capture().NativePartyPresentation!.Rows[targetIndex]!.HitPoints);
            if (revival)
            {
                BattleCombatantSnapshot target = battle.Capture().Combatants.Single(actor => actor.NativeParticipant?.ActorIndex == targetIndex);
                Assert.Equal(7, target.CurrentHp);
                Assert.Equal(0, target.NativeStatus!.Saved & 1);
                Assert.NotNull(target.NativeStatus.Presentation.Saved);
                Assert.Equal(0, target.NativeStatus.Presentation.Saved.GetValueOrDefault() & 1);
            }
            NativeBattleOriginalEffectState finished = Assert.IsType<NativeBattleOriginalEffectState>(battle.NativeGraphics!.OriginalEffect);
            Assert.Equal(effectId, finished.Binding.Effect);
            Assert.Equal(profile.Entry, finished.Binding.Entry);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            uint[] ids = [resources.SoundAppearance, resources.VoiceAppearance, resources.SoundDogTime44, resources.VoiceDeparture];
            NativeBattleModuleSound[] emitted = battle.Events.Skip(sourceEvents).Where(item => item.NativeModuleSound is { } sound && ids.Contains(sound.Sound)).Select(item => item.NativeModuleSound!).ToArray();
            Assert.Equal(ids, emitted.Select(item => item.Sound).ToArray());
            Assert.Equal(new uint[] { 0, 0, 0, 0 }, emitted.Select(item => item.Flags).ToArray());
            Assert.All(emitted, sound =>
            {
                Assert.Equal(128u, sound.RawPan ?? (uint?)sound.Pan);
                Assert.Equal(127u, sound.Volume);
                Assert.NotNull(sound.Allocation);
            });
            Assert.Equal(4, heard.Count(sound => ids.Contains(sound.Sound)));
            BattleSnapshot completed = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, "completed", completed, scene.Render(battle, 320, 224));
            BattleSession restored = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            restored.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224).ToArray());
            audio.Update(restored);
            Assert.Equal(4, heard.Count(sound => ids.Contains(sound.Sound)));
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
            object report = new { Status = status, FirstBlocker = blocker, Effect = effectId, Scenario = revival ? "Controlled post-formula revival; logical target already holds seven HP while prior displayed HP/status retain KO" : "Controlled post-formula seven-HP healing", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), Callbacks = callbacks, DiagnosticMaximumCallbacks = maximum, ContactObservableTransitions = contactTransitions, Completions = completions, SawModelTim = sawModelTim, StageCleared = stageCleared, StagePeak = stagePeak, StageDepths = stageDepths, Boundaries = boundaries, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native differential comparison", "Hardware audibility", "Other targets/headers", "Formula and ordinary command admission", "Common frame packet count includes ordinary actor/stage packets" } };
            using FileStream stream = new(Path.Combine(output, "result.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(stream, report, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static string? BoundaryName(NativeBattleOriginalEffectState? effect, HashSet<string> requested, NativeBattleAngeloRestorationProfile profile)
    {
        NativeBattleModulePoolTask? model = Live(effect, profile.Code(0x7544E0));
        if (requested.Contains("model-start") && model?.ReadByte(0x29) == 1)
        {
            return "model-start";
        }
        if (requested.Contains("program-live") && Live(effect, profile.Code(0x754950))?.ReadDword(0x98) == 1)
        {
            return "program-live";
        }
        NativeBattleModulePoolTask? fade = Live(effect, profile.Code(0x754E60));
        if (fade is not null)
        {
            string name = "fade-" + fade.ReadWord(0x24);
            if (requested.Contains(name))
            {
                return name;
            }
        }
        return null;
    }

    private static NativeBattleModulePoolTask? Live(NativeBattleOriginalEffectState? effect, uint callback) => effect?.Pools.SelectMany(pool => pool.Slots).SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == callback);
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot, NativeBattleAngeloRestorationProfile profile) => snapshot.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: not null } effect && effect.Binding.Effect == profile.Effect && effect.Binding.Entry == profile.Entry ? effect : null;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private sealed record Boundary(string Name, long BeforeCallback, long AfterCallback, int PrivatePackets, int CommonFramePackets, int ChangedRgbPixels, string RgbaSha256);
}
