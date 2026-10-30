using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffectPresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Theory]
    [InlineData((ushort)35, (byte)2, "Life", 0x8649C0u, 300021u, true, false)]
    [InlineData((ushort)5, (byte)4, "Phoenix Down", 0x8D4450u, 300021u, false, false)]
    [InlineData((ushort)4, (byte)2, "Double", 0x8D5080u, 300027u, true, true)]
    [InlineData((ushort)21, (byte)2, "Triple", 0x89DEF0u, 300027u, true, true)]
    [InlineData((ushort)53, (byte)4, "Mega-Potion", 0x811F70u, 300170u, false, false)]
    [InlineData((ushort)58, (byte)4, "Elixir", 0x7F7CB0u, 300182u, false, false)]
    [InlineData((ushort)59, (byte)4, "Megalixir", 0x7F2810u, 300182u, false, false)]
    public void Installed_original_effect_runs_cast_pixels_sound_and_one_contact_through_a_saved_midpoint(ushort effect, byte command, string name, uint entry, uint sound, bool requireCasting, bool applyKernelStatuses)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original effect resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.NativeAction is { } native && native.Command == command && native.MagicId == effect);
        Assert.Equal(name, action.Name);
        BattleSession battle = NativeBattleRestorationPresentationTests.PrepareComponent(game, assets, action, effect, applyKernelStatuses);
        RunCurrentCast(battle.Capture(), assets, effect, entry, sound, requireCasting, applyKernelStatuses);
    }

    [Fact]
    public void Named_standalone_Life_cast_continues_without_replaying_entry_or_the_formula()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EFFECT_PRESENTATION_SNAPSHOT") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_LIFE_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "A concrete standalone effect cast is required.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
        NativeBattleEffectBinding binding = source.NativeGraphics?.OriginalEffect?.Binding ?? Assert.Single(source.Definition.NativeEffectCatalog, binding => binding.LifeResources is not null || binding.DoubleResources is not null || binding.TripleResources is not null || binding.RecoveryResources is not null);
        ushort effect = binding.Effect;
        Assert.True(effect is 4 or 5 or 21 or 35 or 53 or 58 or 59);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        uint sound = binding.RecoveryResources?.Sound ?? (effect is 4 or 21 ? 300027u : 300021u);
        RunCurrentCast(source, assets, effect, binding.Entry, sound, false, false);
    }

    [Fact]
    public void Resume_the_named_revival_checkpoint_for_one_native_callback()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EFFECT_PRESENTATION_SNAPSHOT") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_LIFE_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "A concrete standalone effect checkpoint is required.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
        Assert.True(source.NativeGraphics?.OriginalEffect?.Binding.Effect is 4 or 5 or 21 or 35 or 53 or 58 or 59);
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(first.Capture()));
        first.AdvanceNativeGraphicsPresentation(Callback);
        second.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Equal(source.NativeGraphics!.Callback + 1, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(second, 320, 224).ToArray());
        string id = "effect-next-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        NativeBattleRestorationPresentationTests.Publish(EvidenceDirectory(), id, first.Capture(), pixels);
        Console.WriteLine($"Effect{source.NativeGraphics.OriginalEffect!.Binding.Effect} saved{source.Frame}/{source.NativeGraphics.Callback}, next{first.Frame}/{first.NativeGraphics.Callback}; independent JSON/pixels agree.");
    }

    private static void RunCurrentCast(BattleSnapshot initial, BattleAssets assets, ushort effect, uint entry, uint expectedSound, bool requireCasting, bool requireMorphAndFlash)
    {
        BattleSession battle = BattleSession.Restore(initial);
        Assert.True(HasDescriptor(battle));
        BattleScenePresentation scene = new(assets, initial.Definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(battle);
        string? evidence = EvidenceDirectory();
        string id = $"effect{effect}-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        NativeBattleRestorationPresentationTests.Publish(evidence, id + "-before", initial, scene.Render(battle, 320, 224));
        bool casting = initial.NativeGraphics!.ActionGraphics?.Callback == 0x50A9A0;
        bool selected = initial.NativeGraphics.OriginalEffect?.Binding.Entry == entry;
        bool restoredMidpoint = false;
        int statusTransitions = 0;
        int hpTransitions = 0;
        NativeBattleStatusSnapshot targetStatus = initial.Combatants.Single(actor => actor.Id == "target").NativeStatus!;
        NativeBattleStatusMirror expectedStatus = new(targetStatus.Saved, targetStatus.Battle);
        int expectedStatusTransitions = targetStatus.Presentation == expectedStatus ? 0 : 1;
        ushort expectedHp = checked((ushort)initial.Combatants.Single(actor => actor.Id == "target").CurrentHp);
        ushort initialDisplayedHp = initial.NativePartyPresentation!.Rows[1]!.HitPoints;
        int expectedHpTransitions = initialDisplayedHp == expectedHp ? 0 : 1;
        string? initialMorph = requireMorphAndFlash ? MorphJson(initial, effect) : null;
        bool morphed = false;
        bool flashed = false;
        int peakRgb = 0;
        int capturedGeometry = 0;
        BattleSnapshot? peak = null;
        byte[]? peakPixels = null;
        BattleSession? clone = null;
        NativeBattleTextureCopy? expectedContactCopy = null;
        if (targetStatus.Presentation.Saved is ushort priorSaved && (priorSaved & 1) != 0 && (targetStatus.Saved & 1) == 0)
        {
            NativeBattleGraphicsModel model = initial.NativeGraphics.Models[1];
            NativeBattleGraphicsResource resource = initial.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == "target").Resource;
            expectedContactCopy = NativeBattleTextureMotion.Plan50C780(resource.TextureAnimations, model.TextureAllocationMask, 0, 0);
            Assert.NotNull(expectedContactCopy);
        }
        for (int callback = 0; callback < 1024 && HasDescriptor(battle); callback++)
        {
            BattleSnapshot before = battle.Capture();
            try
            {
                battle.AdvanceNativeGraphicsPresentation(Callback);
                audio.Update(battle);
                audio.Update(battle);
                BattleSnapshot after = battle.Capture();
                NativeBattleGraphicsState graphics = after.NativeGraphics!;
                if (effect is 5 or 35)
                {
                    capturedGeometry += NativeBattleRecoveryCaptureAssertions.AssertOwnedGeometry(after, effect);
                }
                casting |= graphics.ActionGraphics?.Callback == 0x50A9A0;
                selected |= graphics.OriginalEffect?.Binding.Entry == entry;
                NativeBattleStatusMirror priorStatus = before.Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation;
                NativeBattleStatusMirror currentStatus = after.Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation;
                if (priorStatus != currentStatus)
                {
                    Assert.Equal(expectedStatus, currentStatus);
                    uint projectedStatus = initial.Definition.NativeGraphics!.Rules.Project(targetStatus.Saved, targetStatus.Battle);
                    Assert.Equal(projectedStatus, graphics.Models[1].Flags8!.Value & projectedStatus);
                    if (expectedContactCopy is { } copy)
                    {
                        NativeBattleEffectVramCopy expected = new(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
                        Assert.Contains(after.NativeEffectMemory!.Textures!.Slots, slot => slot?.Kind == 3 && slot.Copy == expected);
                    }
                    statusTransitions++;
                }
                ushort priorHp = before.NativePartyPresentation!.Rows[1]!.HitPoints;
                ushort currentHp = after.NativePartyPresentation!.Rows[1]!.HitPoints;
                if (priorHp != currentHp)
                {
                    Assert.Equal(initialDisplayedHp, priorHp);
                    Assert.Equal(expectedHp, currentHp);
                    hpTransitions++;
                }
                if (clone is not null)
                {
                    clone.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, initial.Definition, true).Render(clone, 320, 224));
                    restoredMidpoint = true;
                    clone = null;
                }
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
                flashed |= frame.LastCommands.Any(command => command.Command.Opcode == 0x62);
                if (requireMorphAndFlash && !morphed)
                {
                    morphed = initialMorph != MorphJson(after, effect);
                }
                if (!frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    continue;
                }
                byte[] visible = scene.Render(battle, 320, 224).ToArray();
                NativeBattlePrimitiveFrame plainFrame = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                BattleSession plain = BattleSession.Restore(after with { NativeGraphics = graphics with { PrimitiveFrame = plainFrame } });
                byte[] background = scene.Render(plain, 320, 224).ToArray();
                int rgb = Enumerable.Range(0, visible.Length / 4).Count(pixel => !visible.AsSpan(pixel * 4, 3).SequenceEqual(background.AsSpan(pixel * 4, 3)));
                if (rgb > peakRgb)
                {
                    peakRgb = rgb;
                    peak = after;
                    peakPixels = visible;
                }
                if (rgb > 0 && !restoredMidpoint && clone is null)
                {
                    NativeBattleRestorationPresentationTests.Publish(evidence, id + "-mid", after, visible);
                    string json = string.IsNullOrWhiteSpace(evidence) ? JsonSerializer.Serialize(after) : File.ReadAllText(Path.Combine(evidence, id + "-mid.battle.json"));
                    clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
                    Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(visible, new BattleScenePresentation(assets, initial.Definition, true).Render(clone, 320, 224));
                }
            }
            catch (Exception error)
            {
                NativeBattleRestorationPresentationTests.Publish(evidence, id + "-before-unsupported", before, null);
                if (!string.IsNullOrWhiteSpace(evidence))
                {
                    File.WriteAllText(Path.Combine(evidence, id + "-failure.txt"), error.ToString());
                }
                throw;
            }
        }
        Assert.True(casting || !requireCasting);
        Assert.True(selected);
        Assert.True(restoredMidpoint);
        Assert.True(peakRgb > 0, "Original effect packets must change RGB pixels.");
        Assert.True(morphed || !requireMorphAndFlash);
        Assert.True(flashed || !requireMorphAndFlash);
        Assert.Equal(expectedStatusTransitions, statusTransitions);
        Assert.Equal(expectedHpTransitions, hpTransitions);
        Assert.False(HasDescriptor(battle));
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal((byte)0, battle.NativeEffectMemory!.Active);
        Assert.Equal(0, battle.NativeGraphics.Models[1].Flags0 & 4);
        Assert.All(battle.NativeStage!.Groups, group => Assert.Equal((short)0, group.ColorDepth));
        if (effect is 5 or 35)
        {
            Assert.True(capturedGeometry > 0);
        }
        Assert.Equal(expectedStatus, battle.Capture().Combatants.Single(actor => actor.Id == "target").NativeStatus!.Presentation);
        Assert.Equal(expectedHp, battle.Capture().NativePartyPresentation!.Rows[1]!.HitPoints);
        Assert.Equal(initial.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(initial.Random, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
        NativeBattleModuleSound sound = Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == expectedSound).NativeModuleSound!;
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Equal(initial.Events.Any(entry => entry.NativeModuleSound?.Sound == sound.Sound) ? 0 : 1, heard.Count(receipt => receipt.Sound == sound.Sound));
        int heardBeforeRestore = heard.Count;
        audio.Update(BattleSession.Restore(battle.Capture()));
        Assert.Equal(heardBeforeRestore, heard.Count);
        NativeBattleRestorationPresentationTests.Publish(evidence, id + "-peak", peak!, peakPixels);
        NativeBattleRestorationPresentationTests.Publish(evidence, id + "-after", battle.Capture(), scene.Render(battle, 320, 224));
        Console.WriteLine($"Effect{effect} peakRGB{peakRgb} at{peak!.Frame}/{peak.NativeGraphics!.Callback}, after{battle.Frame}/{battle.NativeGraphics.Callback}, status{statusTransitions}, HP{hpTransitions}, morph{morphed}, flash{flashed}; evidence{evidence ?? "disabled"}.");
    }

    private static bool HasDescriptor(BattleSession battle)
    {
        NativeBattleDescriptorState descriptors = battle.NativeGraphics!.Descriptors;
        return descriptors.Queue.Any(slot => descriptors.Packets[slot].Command == 104);
    }

    private static string MorphJson(BattleSnapshot snapshot, ushort effect)
    {
        object? owner = effect switch
        {
            4 => snapshot.NativeEffectMemory!.DoubleMorph,
            21 => snapshot.NativeEffectMemory!.TripleMorph,
            _ => throw new NotSupportedException("The current enhancement test requires its own original morph owner.")
        };
        Assert.NotNull(owner);
        return JsonSerializer.Serialize(owner);
    }

    private static string? EvidenceDirectory()
    {
        return Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EFFECT_PRESENTATION_EVIDENCE") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_LIFE_EVIDENCE");
    }
}
