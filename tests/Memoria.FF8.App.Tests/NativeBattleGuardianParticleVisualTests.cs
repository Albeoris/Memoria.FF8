using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianParticleVisualTests
{
    [Fact]
    public void Named_first_particle_frame_changes_pixels_when_only_its_native_private_trail_packets_are_removed()
    {
        string? beforePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PARTICLE_VISUAL_BEFORE");
        string? afterPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PARTICLE_VISUAL_AFTER");
        Assert.SkipUnless(beforePath is not null && afterPath is not null, "Concrete standalone snapshots before and after the first particle callback are required.");
        BattleSnapshot before = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(beforePath!))!;
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(afterPath!))!;
        Assert.Equal((ushort)116, snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(before.Frame + 1, snapshot.Frame);
        Assert.Equal(before.NativeGraphics!.Callback + 1, snapshot.NativeGraphics.Callback);
        Assert.DoesNotContain(before.NativeGraphics.PrimitiveFrame!.LastCommands, IsParticle);
        NativeBattlePrimitiveFrame frame = snapshot.NativeGraphics.PrimitiveFrame!;
        int particleCommands = frame.LastCommands.Count(IsParticle);
        Assert.True(particleCommands > 0);
        BattleSnapshot without = snapshot with { NativeGraphics = snapshot.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !IsParticle(command)).ToImmutableArray() } } };
        BattleSession original = BattleSession.Restore(snapshot);
        BattleSession control = BattleSession.Restore(without);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        byte[] full = scene.Render(original, 320, 224).ToArray();
        byte[] removed = scene.Render(control, 320, 224).ToArray();
        int changed = Enumerable.Range(0, 320 * 224).Count(index => !full.AsSpan(4 * index, 3).SequenceEqual(removed.AsSpan(4 * index, 3)));
        BattleSnapshot onlyParticles = snapshot with { NativeGraphics = snapshot.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(IsParticle).ToImmutableArray() } } };
        byte[] isolated = scene.Render(BattleSession.Restore(onlyParticles), 320, 224);
        int isolatedPixels = Enumerable.Range(0, 320 * 224).Count(index => isolated[4 * index] != 0 || isolated[4 * index + 1] != 0 || isolated[4 * index + 2] != 0);
        Console.WriteLine($"Private trail diagnostic: commands{particleCommands}, isolatedRGBpixels{isolatedPixels}, full-frameRGBdifference{changed}.");
        Assert.True(changed > 0, "Actual6C84C0 private packets must contribute visible pixels independently of common ribbons and GF skin.");
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(original.Capture()));
        Assert.Equal(JsonSerializer.Serialize(without), JsonSerializer.Serialize(control.Capture()));
        Console.WriteLine($"Native particle visual control at frame{snapshot.Frame}/callback{snapshot.NativeGraphics.Callback}: before signature0, removed{particleCommands} private trail draws, changed{changed}RGB pixels; neither simulation advanced.");
    }

    private static bool IsParticle(NativeBattleLogicalResolvedCommand command)
    {
        //6C84C0/6C8620 fixed stripe UV/CLUT/page, shared with common ribbons but distinct by the actual effect-arena bank.
        ImmutableArray<uint> words = command.Command.Words;
        return command.Pass > 0 && command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0x3E && words.Length == 12 && words[2] == 0x3E94C008 && words[5] == 0x00B6C038 && (words[8] & 0xFFFF) == 0xC008 && (words[11] & 0xFFFF) == 0xC038;
    }
}
