using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRush91PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_Rush_completes_original_resources_with_independent_live_restores()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_RUSH91_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_RUSH91_SNAPSHOT to a complete queued controlled post-formula Rush fixture.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RUSH91_OUTPUT") ?? throw new InvalidDataException("A distinct MEMORIA_FF8_RUSH91_OUTPUT directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(), "Output must be a distinct empty directory.");
        Directory.CreateDirectory(output);
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_RUSH91_MAX_CALLBACKS") is { } supplied ? int.Parse(supplied) : 512;
        Assert.True(maximum > 0, "The diagnostic callback budget must be positive.");
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
        Assert.Equal((ushort)11, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)91, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.True(graphics.ActionGraphics is null || graphics.ActionGraphics.HeaderIndex == headerIndex);
        BattleNativeAction kernel = Assert.Single(assets.NativeActions, action => action.Command == 240 && action.ActionId == 11);
        Assert.Equal((ushort)91, kernel.MagicId);
        NativeBattleContactPacket contact = packets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("The fixture lacks its explicit contact owner."));
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal(0, contact.Primary.HitFlags3 & 1);
        Assert.Equal((byte)1, contact.Primary.Flags2);
        Assert.Equal(0, contact.Primary.ContactFlags & 0x1000);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        Assert.Equal((byte)4, graphics.Models[0].ModelId4);
        string targetId = graphics.ActorIds[3]!;
        int targetPostFormulaHp = source.Combatants.Single(actor => actor.Id == targetId).CurrentHp;
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([91])[90];
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[90]));
        NativeBattleRush91Resources resources = installed.Rush91Resources!;
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        heard.Clear();
        int sourceEvents = battle.Events.Count;
        HashSet<string> requested = ["model-live", "particles-live", "program-live", "model-height", "overlay-live", "nodes-live", "departure"];
        List<Boundary> boundaries = [];
        int callbacks = 0;
        int contacts = 0;
        int completions = 0;
        int emissionSequences = 0;
        bool initialized = false;
        bool sawModelTim = false;
        bool sawPrivate = false;
        bool sawCommon = false;
        bool sawOverlay = false;
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
                string? boundary = BoundaryName(beforeEffect, requested);
                BattleSession? cold = null;
                if (boundary is not null)
                {
                    NativeBattleRestorationPresentationTests.Publish(output, boundary + "-before", prior, scene.Render(battle, 320, 224));
                    cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, boundary + "-before.battle.json")));
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
                bool contactSequence = prior.NativeGraphics.Models[3].Sequence != contact.Primary.Sequence && current.NativeGraphics.Models[3].Sequence == contact.Primary.Sequence;
                bool targetAdvanced = Tasks(beforeEffect, 0x759770).Any(task => task.ReadByte(0x29) == 1) && Tasks(afterEffect, 0x759770).Any(task => task.ReadByte(0x29) == 2);
                Assert.Equal(targetAdvanced, contactSequence);
                if (contactSequence)
                {
                    NativeBattleModulePoolTask beforeTarget = Assert.Single(Tasks(beforeEffect, 0x759770), task => task.ReadByte(0x29) == 1);
                    NativeBattleModulePoolTask afterTarget = Assert.Single(Tasks(afterEffect, 0x759770), task => task.ReadByte(0x29) == 2);
                    Assert.Equal(unchecked((ushort)(beforeTarget.ReadWord(0x24) + 1)), afterTarget.ReadWord(0x24));
                    contacts++;
                }
                Assert.DoesNotContain(current.NativeGraphics.Effects.Slots, task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 3);
                Assert.Equal(targetPostFormulaHp, current.Combatants.Single(actor => actor.Id == targetId).CurrentHp);
                Assert.Equal(JsonSerializer.Serialize(source.NativePartyPresentation), JsonSerializer.Serialize(current.NativePartyPresentation));
                Assert.Equal(graphics.Models[3].ActiveStagedResourceRow100, current.NativeGraphics.Models[3].ActiveStagedResourceRow100);
                int newbornSprites = Tasks(afterEffect, 0x7611B0).Count(task => task.ReadWord(0x24) == 1);
                if (newbornSprites > 0)
                {
                    Assert.Equal(0, newbornSprites % 4);
                    int emitted = newbornSprites / 4;
                    Assert.Equal(emitted * 2, Tasks(afterEffect, 0x75EEC0).Count(task => task.ReadWord(0x24) == 1));
                    NativeCrtRandomState expected = prior.NativeCrtRandom ?? throw new InvalidDataException("The fixture lacks its CRT stream.");
                    for (int draw = 0; draw < emitted * 28; draw++)
                    {
                        expected = expected.Draw(out _);
                    }
                    Assert.Equal(expected, current.NativeCrtRandom);
                    emissionSequences += emitted;
                }
                sawModelTim |= current.NativeEffectMemory?.Textures?.Slots.Any(item => item?.Source?.Identity == resources.ModelTim.Identity) == true;
                NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                NativeBattleReachedCaptureAssertions.Moving(91, current, frame.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8"));
                sawPrivate |= frame.LastCommands.Any(item => item.Command.Packet.Bank == "20DFAB8");
                sawCommon |= frame.LastCommands.Any(item => item.Command.Packet.Bank is "off_B6D070" or "off_B6D094");
                sawOverlay |= frame.LastCommands.Any(item => item.Command.Packet.Bank == NativeBattleFullscreenOverlay.PacketBank);
                if (cold is not null)
                {
                    cold.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold.Capture()));
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                    NativeBattlePrimitiveFrame removed = frame with { LastCommands = frame.LastCommands.Where(item => item.Command.Packet.Bank != "20DFAB8" && item.Command.Packet.Bank != NativeBattleFullscreenOverlay.PacketBank).ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = removed } });
                    byte[] background = scene.Render(plain, 320, 224).ToArray();
                    int changed = Enumerable.Range(0, pixels.Length / 4).Count(pixel => !pixels.AsSpan(pixel * 4, 3).SequenceEqual(background.AsSpan(pixel * 4, 3)));
                    NativeBattleRestorationPresentationTests.Publish(output, boundary! + "-after", current, pixels);
                    boundaries.Add(new(boundary!, prior.NativeGraphics.Callback, current.NativeGraphics.Callback, changed, Convert.ToHexString(SHA256.HashData(pixels))));
                    requested.Remove(boundary!);
                }
            }
            Assert.True(initialized);
            Assert.Empty(requested);
            Assert.Equal(7, boundaries.Count);
            Assert.Contains(boundaries, item => item.ChangedRgbPixels > 0);
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.True(emissionSequences > 0);
            Assert.True(sawModelTim && sawPrivate && sawCommon && sawOverlay);
            NativeBattleOriginalEffectState finished = Assert.IsType<NativeBattleOriginalEffectState>(battle.NativeGraphics!.OriginalEffect);
            Assert.Equal((ushort)91, finished.Binding.Effect);
            Assert.Equal(0x759370u, finished.Binding.Entry);
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeEffectMemory!.Active);
            NativeBattleModuleSound sound = Assert.Single(battle.Events.Skip(sourceEvents), item => item.NativeModuleSound?.Sound == resources.SoundAppearance).NativeModuleSound!;
            Assert.Equal(0u, sound.Flags);
            Assert.Equal(128u, sound.RawPan ?? (uint?)sound.Pan);
            Assert.Equal(127u, sound.Volume);
            Assert.NotNull(sound.Allocation);
            Assert.Single(heard, item => item.Sound == resources.SoundAppearance);
            BattleSnapshot completed = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, "completed", completed, scene.Render(battle, 320, 224));
            BattleSession restored = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.AdvanceNativeGraphicsPresentation(Callback);
            restored.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224).ToArray());
            audio.Update(restored);
            Assert.Single(heard, item => item.Sound == resources.SoundAppearance);
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
            object report = new { Status = status, FirstBlocker = blocker, Scenario = "Controlled post-formula seven-point enemy contact; logical HP is already committed and contact flag1 suppresses damage text", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), Callbacks = callbacks, DiagnosticMaximumCallbacks = maximum, ContactSequenceObservableTransitions = contacts, ContactObservation = "Target sequence selection coincides with target task stage1-to2; observable transition count, not a contact call counter", Completions = completions, EmissionSequences = emissionSequences, EmissionCrtDraws = emissionSequences * 28, SawModelTim = sawModelTim, SawPrivate = sawPrivate, SawCommonFramePackets = sawCommon, SawFixedOverlay = sawOverlay, Boundaries = boundaries, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole-entry native differential comparison", "Hardware audibility", "Other targets/headers", "Formula and ordinary command admission", "Common frame packets include actor/stage output" } };
            using FileStream stream = new(Path.Combine(output, "result.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(stream, report, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static string? BoundaryName(NativeBattleOriginalEffectState? effect, HashSet<string> requested)
    {
        NativeBattleModulePoolTask? model = Tasks(effect, 0x75E910).FirstOrDefault();
        int phase = model is null ? -1 : model.Payload[0x29 - 12];
        if (requested.Contains("overlay-live") && Tasks(effect, 0x8DDEC0).Any())
        {
            return "overlay-live";
        }
        if (requested.Contains("model-live") && phase == 1)
        {
            return "model-live";
        }
        if (requested.Contains("particles-live") && Tasks(effect, 0x7611B0).Any(task => task.ReadWord(0x24) >= 2) && Tasks(effect, 0x75EEC0).Any())
        {
            return "particles-live";
        }
        if (requested.Contains("program-live") && Tasks(effect, 0x761B00).Any(task => task.ReadDword(0x98) > 0))
        {
            return "program-live";
        }
        if (requested.Contains("model-height") && phase == 4)
        {
            return "model-height";
        }
        if (requested.Contains("nodes-live") && Tasks(effect, 0x759B40).Any(task => task.Payload[0x29 - 12] == 1))
        {
            return "nodes-live";
        }
        return requested.Contains("departure") && phase == 8 ? "departure" : null;
    }

    private static IEnumerable<NativeBattleModulePoolTask> Tasks(NativeBattleOriginalEffectState? effect, uint callback) => effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0 && task.Callback == callback) ?? [];
    private static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: not null, Binding.Effect: 91, Binding.Entry: 0x759370 } effect ? effect : null;
    private static bool HasAction(BattleSession battle) => battle.NativeGraphics!.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
    private sealed record Boundary(string Name, long BeforeCallback, long AfterCallback, int ChangedRgbPixels, string RgbaSha256);
}
