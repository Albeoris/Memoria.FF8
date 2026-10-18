using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCactuar199PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_Cactuar_completes_original_resources_and_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_CACTUAR199_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_CACTUAR199_SNAPSHOT to a complete queued controlled post-formula Cactuar fixture.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CACTUAR199_OUTPUT") ?? throw new InvalidDataException("A distinct MEMORIA_FF8_CACTUAR199_OUTPUT directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(), "Output must be a distinct empty directory.");
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_CACTUAR199_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 1024;
        Assert.True(maximum > 0);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction kernel = Assert.Single(assets.NativeActions, action => action.MagicId == 199);
        Assert.Equal((byte)254, kernel.Command);
        Assert.Equal((ushort)77, kernel.ActionId);
        Assert.Equal(2, kernel.KernelSection);
        Assert.Equal(13, kernel.KernelIndex);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        BattleActionDefinition guardianAction = Assert.Single(source.Definition.ActionDefinitions, action => action.NativeAction?.MagicId == 199);
        Assert.Equal(20, guardianAction.Level);
        Assert.Equal((byte)29, kernel.AttackType);
        Assert.Equal((byte)90, kernel.Power);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        int queued = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
        uint offset = unchecked(graphics.Descriptors.Packets[queued].Argument - 0x1D280C4u);
        Assert.True(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        int headerIndex = checked((int)(offset / 20));
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[headerIndex];
        Assert.Equal(kernel.Command, header.ReadByte(1));
        Assert.Equal(kernel.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)199, NativeBattleActionGraphics.Word(header, 6));
        Assert.True(graphics.ActionGraphics is null || graphics.ActionGraphics.HeaderIndex == headerIndex);
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)1, contact.Primary.Flags2);
        Assert.Equal(0, contact.Primary.ContactFlags & 0x1000);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        string targetId = graphics.ActorIds[3]!;
        NativeBattleGraphicsResource targetResource = Assert.Single(source.Definition.NativeGraphics!.Bindings, binding => binding.ActorId == targetId).Resource;
        int targetInitialHp = source.Combatants.Single(actor => actor.Id == targetId).CurrentHp;
        Assert.Equal(10000, targetInitialHp);
        Assert.False(source.NativeActionScratch!.GuardianFormulaE1E);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([199])[198];
        Assert.Equal(0x5A8750u, installed.Entry);
        Assert.Equal(0x5A8730u, installed.Initializer);
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[198]));
        NativeBattleCactuar199Resources resources = installed.Cactuar199Resources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        int soundStops = 0;
        List<NativeBattleMusicCommandReceipt> musicCommands = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add, stopSounds: () => soundStops++, playMusicCommand: musicCommands.Add);
        audio.Update(battle);
        heard.Clear();
        int sourceEvents = battle.Events.Count;
        HashSet<string> requested = ["guardian-formula", "pose0", "pose1", "pose3", "pose2-particles", "pose4-cache", "needles-state", "flash-live", "common-io"];
        List<Boundary> boundaries = [];
        HashSet<byte> poses = [];
        HashSet<string> cameras = [];
        List<uint> streams = [];
        int callbacks = 0;
        int completions = 0;
        int contactSchedulerTransitions = 0;
        int timelineMaximum = 0;
        int bodyMaximum = 0;
        int hitMaximum = 0;
        int maximumParticles = 0;
        int maximumNeedles = 0;
        int needlePackets = 0;
        int stageFlashPeak = 0;
        int delayedHitCallbacks = 0;
        int formulaTransitions = 0;
        int contactTextTransitions = 0;
        ushort? formulaAmount = null;
        FormulaBoundary? formulaBoundary = null;
        byte? requestedContactSequence = null;
        byte? executedContactSequence = null;
        int? vibrationHandle = null;
        bool initialized = false;
        bool sawVibration = false;
        bool sawHeapTim = false;
        bool sawModelTim = false;
        bool sawCache = false;
        bool sawCommonIo = false;
        bool sawCommonBytes = false;
        bool sawCommonPackets = false;
        bool sawFullscreenFlash = false;
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
                NativeBattleContactPacket actualContact = current.NativeActionPackets!.ReadContact(header.FirstContactIndex.Value);
                if (prior.NativeActionScratch!.GuardianFormulaE1E != true && current.NativeActionScratch!.GuardianFormulaE1E == true)
                {
                    Assert.Equal((ushort)7, prior.NativeActionPackets!.ReadContact(header.FirstContactIndex.Value).Primary.Amount6);
                    Assert.True(actualContact.Primary.Amount6 > 7);
                    Assert.Equal((ushort)2000, actualContact.Primary.Amount6);
                    Assert.Equal((byte)3, actualContact.Primary.ActorIndex);
                    Assert.Equal(0, actualContact.Primary.HitFlags3 & 1);
                    int beforeHp = prior.Combatants.Single(actor => actor.Id == targetId).CurrentHp;
                    int afterHp = current.Combatants.Single(actor => actor.Id == targetId).CurrentHp;
                    Assert.Equal(targetInitialHp, beforeHp);
                    Assert.Equal(beforeHp - actualContact.Primary.Amount6, afterHp);
                    formulaAmount = actualContact.Primary.Amount6;
                    formulaBoundary = new(prior.NativeGraphics.Callback, current.NativeGraphics.Callback, beforeHp, afterHp, actualContact.Primary.Amount6, actualContact.Primary.ContactFlags, prior.NativeCrtRandom, current.NativeCrtRandom);
                    formulaTransitions++;
                }
                initialized |= afterEffect?.ReturnedDescriptor is > 0;
                if (beforeEffect?.ReturnedDescriptor is > 0 && afterEffect?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                timelineMaximum = Math.Max(timelineMaximum, MaximumCounter(afterEffect, 0x5A8940));
                bodyMaximum = Math.Max(bodyMaximum, MaximumCounter(afterEffect, 0x5A8C20));
                hitMaximum = Math.Max(hitMaximum, MaximumCounter(afterEffect, 0x5AA2A0));
                foreach (NativeBattleModulePoolTask hit in Tasks(beforeEffect, 0x5AA2A0))
                {
                    NativeBattleModulePoolTask nextHit = Assert.Single(AllTasks(afterEffect, 0x5AA2A0), next => next.ReadWord(0x18) == hit.ReadWord(0x18));
                    if (unchecked((short)hit.ReadWord(0xE)) > 0)
                    {
                        Assert.Equal(unchecked((ushort)(hit.ReadWord(0xE) - 1)), nextHit.ReadWord(0xE));
                        Assert.Equal(hit.ReadWord(0xC), nextHit.ReadWord(0xC));
                        delayedHitCallbacks++;
                    }
                    else
                    {
                        Assert.Equal(unchecked((ushort)(hit.ReadWord(0xC) + 1)), nextHit.ReadWord(0xC));
                    }
                    if (hit.ReadWord(0xE) == 0 && hit.ReadWord(0xC) == 49)
                    {
                        Assert.Contains(Tasks(afterEffect, 0x5AA2A0), next => next.ReadWord(0xC) == 50 && next.ReadWord(0x18) == hit.ReadWord(0x18));
                        int sequenceStart = targetResource.SequenceStart(actualContact.Primary.Sequence);
                        Assert.Equal((byte)0xA7, targetResource.SequenceSection[sequenceStart]);
                        byte switchedSequence = targetResource.SequenceSection[sequenceStart + 1];
                        Assert.Equal(switchedSequence, current.NativeGraphics.Models[3].Sequence);
                        Assert.Equal(1, current.NativeGraphics.Models[3].SequenceFrame);
                        requestedContactSequence = actualContact.Primary.Sequence;
                        executedContactSequence = switchedSequence;
                        contactSchedulerTransitions++;
                    }
                }
                for (int index = 0; index < current.NativeGraphics.Effects.Slots.Length; index++)
                {
                    NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[index];
                    if ((text.Flags0 & 1) != 0 && (prior.NativeGraphics.Effects.Slots[index].Flags0 & 1) == 0 && text.Kind == NativeBattleEffectKind.DamageText5069B0 && text.ModelIndex == 3)
                    {
                        Assert.NotNull(formulaAmount);
                        NativeBattleEffectsState expected = NativeBattleEffectsState.Clear().PrependDamageText(3, actualContact.Primary.ContactFlags, actualContact.Primary.Amount6);
                        NativeBattleEffectTask expectedText = Assert.Single(expected.Slots, task => (task.Flags0 & 1) != 0);
                        Assert.Equal(expectedText.Glyphs.ToArray(), text.Glyphs.ToArray());
                        contactTextTransitions++;
                    }
                }
                Assert.Equal(targetInitialHp - formulaAmount.GetValueOrDefault(), current.Combatants.Single(actor => actor.Id == targetId).CurrentHp);
                Assert.Equal(JsonSerializer.Serialize(source.NativePartyPresentation), JsonSerializer.Serialize(current.NativePartyPresentation));
                NativeBattleCactuar199State? state = current.NativeEffectMemory?.Cactuar199;
                if (state?.RawModel.DecodedPose is not null)
                {
                    poses.Add(state.RawModel.Model[0x6C]);
                    Assert.Equal(11, state.RawModel.DecodedPose.Bones.Length);
                    Assert.Equal(0x818, state.RawModel.ProjectedScratch!.Value.Length);
                }
                if (state?.CameraTask is not null)
                {
                    cameras.Add(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(current.NativeCamera))));
                }
                if (state?.VibrationHandle is > 0)
                {
                    vibrationHandle = state.VibrationHandle;
                    sawVibration |= current.NativeVibrationProcess!.IsPlaying(state.VibrationHandle.Value);
                }
                uint? stream = current.NativeEffectMemory?.SummonStream?.SourceAddress;
                uint? previousStream = prior.NativeEffectMemory?.SummonStream?.SourceAddress;
                if (stream is 0xE94910 or 0xE94914 && stream != previousStream)
                {
                    streams.Add(stream.Value);
                }
                if (state is not null && afterEffect?.ReturnedDescriptor is > 0)
                {
                    maximumParticles = Math.Max(maximumParticles, ActiveSlots(current, 0x2A000, 0x1C, 100));
                    maximumNeedles = Math.Max(maximumNeedles, ActiveSlots(current, 0x2B000, 0x3C, 800));
                    sawCache |= CacheInitialized(current);
                }
                sawHeapTim |= current.NativeEffectMemory?.Textures?.Slots.Any(item => item?.Source?.Identity == resources.InitializerHeapTim.Identity) == true;
                sawModelTim |= current.NativeEffectMemory?.Textures?.Slots.Any(item => item?.Source?.Identity == resources.ModelTim.Identity) == true;
                sawCommonIo |= CommonIo(current);
                if (CommonIo(current))
                {
                    Assert.Equal(source.Definition.NativeCommonEffectTexture!.Data.ToArray(), current.NativeEffectMemory!.Arena!.Read(0x4000, source.Definition.NativeCommonEffectTexture.Data.Length));
                    sawCommonBytes = true;
                }
                NativeBattlePrimitiveFrame primitive = current.NativeGraphics.PrimitiveFrame!;
                stageFlashPeak = Math.Max(stageFlashPeak, current.NativeStage!.Groups.Max(group => group.ColorDepth));
                sawFullscreenFlash |= current.NativeGraphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300);
                sawCommonPackets |= primitive.LastCommands.Any(item => item.Command.Packet.Bank is "off_B6D070" or "off_B6D094");
                needlePackets += primitive.LastCommands.Count(item => item.Command.Opcode == 0x2E && item.Command.Words.Length == 9 && item.Command.Words[2] == 0x3F540000 && item.Command.Words[4] == 0x00B9000F);
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
            Assert.Equal(9, boundaries.Count);
            Assert.Equal(1, formulaTransitions);
            Assert.NotNull(formulaBoundary);
            bool textSuppressed = (formulaBoundary!.ContactFlags & 1) != 0 && (formulaBoundary.ContactFlags & 0x1000) == 0;
            Assert.Equal(textSuppressed ? 0 : 1, contactTextTransitions);
            Assert.Equal(1, completions);
            Assert.Equal(1, contactSchedulerTransitions);
            Assert.Equal(152, timelineMaximum);
            Assert.Equal(120, bodyMaximum);
            Assert.Equal(55, hitMaximum);
            Assert.InRange(delayedHitCallbacks, 0, 7);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, poses.Order().ToArray());
            Assert.True(cameras.Count > 2);
            Assert.Equal(new uint[] { 0xE94910, 0xE94914 }, streams.ToArray());
            Assert.True(sawHeapTim && sawModelTim && sawCache && sawCommonIo && sawCommonBytes && sawCommonPackets);
            Assert.True(maximumParticles > 0 && maximumNeedles > 0);
            Assert.True(sawVibration);
            Assert.True(sawFullscreenFlash);
            Assert.Equal(2048, stageFlashPeak);
            Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
            Assert.NotNull(vibrationHandle);
            Assert.False(battle.Capture().NativeVibrationProcess!.IsPlaying(vibrationHandle.GetValueOrDefault()));
            NativeBattleOriginalEffectState finished = battle.NativeGraphics!.OriginalEffect!;
            Assert.Equal((ushort)199, finished.Binding.Effect);
            Assert.Equal(0x5A8750u, finished.Binding.Entry);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            Assert.True(battle.NativeEffectMemory.Cactuar199!.CameraTask!.Complete);
            uint[] sounds = [resources.Sound1, resources.Sound2];
            NativeBattleModuleSound[] emitted = battle.Events.Skip(sourceEvents).Where(item => item.NativeModuleSound is { } sound && sounds.Contains(sound.Sound)).Select(item => item.NativeModuleSound!).ToArray();
            Assert.Equal(sounds, emitted.Select(item => item.Sound).ToArray());
            Assert.All(emitted, sound =>
            {
                Assert.Equal(0u, sound.Flags);
                Assert.Equal(128u, sound.RawPan ?? (uint?)sound.Pan);
                Assert.NotNull(sound.Allocation);
            });
            Assert.Equal(2, heard.Count(sound => sounds.Contains(sound.Sound)));
            uint[] streamSounds = [unchecked(resources.Voice1 + 690000u), unchecked(resources.Voice2 + 690000u)];
            NativeBattleModuleSound[] playedStreams = battle.Events.Skip(sourceEvents).Where(item => item.NativeModuleSound is { } sound && streamSounds.Contains(sound.Sound)).Select(item => item.NativeModuleSound!).ToArray();
            Assert.Equal(streamSounds, playedStreams.Select(sound => sound.Sound).ToArray());
            Assert.All(playedStreams, sound =>
            {
                Assert.Equal(0u, sound.Flags);
                Assert.Equal(128u, sound.RawPan);
                Assert.Equal(96u, sound.Volume);
                Assert.NotNull(sound.Allocation);
            });
            Assert.Equal(2, heard.Count(sound => streamSounds.Contains(sound.Sound)));
            Assert.Equal(battle.Events.Skip(sourceEvents).Count(item => item.NativeStopAllSounds), soundStops);
            Assert.Equal(battle.Events.Skip(sourceEvents).Count(item => item.NativeMusicCommand is not null), musicCommands.Count);
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
            BattleSession restored = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            restored.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224).ToArray());
            audio.Update(restored);
            Assert.Equal(2, heard.Count(sound => sounds.Contains(sound.Sound)));
            Assert.Equal(2, heard.Count(sound => streamSounds.Contains(sound.Sound)));
            Assert.Equal(battle.Events.Skip(sourceEvents).Count(item => item.NativeStopAllSounds), soundStops);
            Assert.Equal(battle.Events.Skip(sourceEvents).Count(item => item.NativeMusicCommand is not null), musicCommands.Count);
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
            object report = new { Status = status, FirstBlocker = blocker, Scenario = "Fresh 10000-HP enemy component; placeholder seven-HP header followed by actual deferred command254 GF formula; ordinary UI admission remains separate", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), Callbacks = callbacks, DiagnosticMaximumCallbacks = maximum, Completions = completions, TimelineMaximum = timelineMaximum, BodyMaximum = bodyMaximum, HitMaximum = hitMaximum, DelayedHitCallbacks = delayedHitCallbacks, FormulaTransitions = formulaTransitions, FormulaBoundary = formulaBoundary, ContactTextTransitions = contactTextTransitions, ContactSchedulerC49Transitions = contactSchedulerTransitions, ContactRequestedSequence = requestedContactSequence, ContactExecutedSequence = executedContactSequence, ContactObservation = "Hit task C49-to50; actual contact selects the original sequence whose A7 immediately switches to the reported executed sequence; native damage-text transition is counted separately", ModelPoses = poses.Order().ToArray(), CameraStates = cameras.Count, StreamSourceTransitions = streams, SoundStopRequests = soundStops, MusicCommandReceipts = musicCommands, StreamPlayback = "Two stream plays at pan128 volume96; no fade/ramp", MaximumParticles = maximumParticles, MaximumNeedles = maximumNeedles, ParsedNeedlePackets = needlePackets, StageFlashPeak = stageFlashPeak, SawFullscreenFlash = sawFullscreenFlash, SawHeapTim = sawHeapTim, SawModelTim = sawModelTim, SawTargetCache = sawCache, SawActualCommonIo = sawCommonIo, SawActualCommonBytes = sawCommonBytes, VibrationHandle = vibrationHandle, Boundaries = boundaries, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native differential visual comparison", "Hardware audibility and vibration", "Other targets/headers", "Ordinary command/UI admission and other formula variants", "Common frame packets include actor/stage output" } };
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string? BoundaryName(BattleSnapshot snapshot, HashSet<string> requested)
    {
        NativeBattleOriginalEffectState? effect = Selected(snapshot);
        NativeBattleCactuar199State? state = snapshot.NativeEffectMemory?.Cactuar199;
        if (requested.Contains("guardian-formula") && snapshot.NativeGuardianBoost?.Phase == 3 && snapshot.NativeActionScratch!.GuardianFormulaE1E != true)
        {
            return "guardian-formula";
        }
        if (state?.RawModel.DecodedPose is not null && Tasks(effect, 0x5A8C20).Any())
        {
            string? pose = state.RawModel.Model[0x6C] switch { 0 => "pose0", 1 => "pose1", 3 => "pose3", _ => null };
            if (pose is not null && requested.Contains(pose))
            {
                return pose;
            }
            if (requested.Contains("pose2-particles") && state.RawModel.Model[0x6C] == 2 && ActiveSlots(snapshot, 0x2A000, 0x1C, 100) > 0)
            {
                return "pose2-particles";
            }
            if (requested.Contains("pose4-cache") && state.RawModel.Model[0x6C] == 4 && CacheInitialized(snapshot))
            {
                return "pose4-cache";
            }
        }
        if (requested.Contains("needles-state") && state is not null && effect?.ReturnedDescriptor is > 0 && ActiveSlots(snapshot, 0x2B000, 0x3C, 800) > 0)
        {
            return "needles-state";
        }
        if (requested.Contains("flash-live") && Tasks(effect, 0x5A8B40).Any(task => task.ReadWord(0xC) >= 2))
        {
            return "flash-live";
        }
        return requested.Contains("common-io") && CommonIo(snapshot) ? "common-io" : null;
    }

    private static int ActiveSlots(BattleSnapshot snapshot, int offset, int stride, int count)
    {
        byte[] bytes = snapshot.NativeEffectMemory!.Arena!.Read(offset, stride * count);
        return Enumerable.Range(0, count).Count(index => (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * stride)) & 1) != 0);
    }

    private static bool CacheInitialized(BattleSnapshot snapshot) => BinaryPrimitives.ReadUInt16LittleEndian(snapshot.NativeEffectMemory!.Arena!.Read(0x2000E, 2)) is >= 1000 and <= 1799;
    private static bool CommonIo(BattleSnapshot snapshot) => snapshot.NativeFileIo?.Rows.Any(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470) == true;
    private static int MaximumCounter(NativeBattleOriginalEffectState? effect, uint callback) => AllTasks(effect, callback).Select(task => (int)task.ReadWord(0xC)).DefaultIfEmpty(0).Max();
    private static IEnumerable<NativeBattleModulePoolTask> AllTasks(NativeBattleOriginalEffectState? effect, uint callback) => effect?.Pools.SelectMany(pool => pool.Slots).Where(task => task.Callback == callback) ?? [];
    private static IEnumerable<NativeBattleModulePoolTask> Tasks(NativeBattleOriginalEffectState? effect, uint callback) => AllTasks(effect, callback).Where(task => (task.Flags & 1) != 0);
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: not null, Binding.Effect: 199, Binding.Entry: 0x5A8750 } effect ? effect : null;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private sealed record Boundary(string Name, long BeforeCallback, long AfterCallback, string RgbaSha256);
    private sealed record FormulaBoundary(long BeforeCallback, long AfterCallback, int BeforeHp, int AfterHp, ushort Amount, ushort ContactFlags, NativeCrtRandomState? BeforeCrt, NativeCrtRandomState? AfterCrt);
}
