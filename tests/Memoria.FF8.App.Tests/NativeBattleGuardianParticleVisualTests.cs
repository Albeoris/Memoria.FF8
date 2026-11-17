using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
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
        (JsonElement before, long beforeCallback, NativeBattlePrimitiveFrame beforeFrame) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(beforePath!, 116);
        (JsonElement source, long callback, NativeBattlePrimitiveFrame frame) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(afterPath!, 116);
        Assert.Equal(before.GetProperty("Frame").GetInt64() + 1, source.GetProperty("Frame").GetInt64());
        Assert.Equal(beforeCallback + 1, callback);
        Assert.DoesNotContain(beforeFrame.LastCommands, IsParticle);
        int particleCommands = frame.LastCommands.Count(IsParticle);
        Assert.True(particleCommands > 0);
        NativeBattlePrimitiveFrame without = frame with { LastCommands = NativeBattleResolvedCommandList.From(frame.LastCommands.Where(command => !IsParticle(command))) };
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleGraphicsDefinition definition = Assert.IsType<NativeBattleGraphicsDefinition>(NativeBattleFixtureState.ReadArchived<NativeBattleGraphicsDefinition>(source.GetProperty("Definition"), "NativeGraphics"));
        NativeBattleEffectMemory memory = Assert.IsType<NativeBattleEffectMemory>(NativeBattleFixtureState.ReadArchived<NativeBattleEffectMemory>(source, "NativeEffectMemory"));
        BattleRenderer renderer = new(320, 224, ViewportLayout.Fit(320, 240, 320, 224));
        byte[] full = Render(frame);
        byte[] removed = Render(without);
        int changed = Enumerable.Range(0, 320 * 224).Count(index => !full.AsSpan(4 * index, 3).SequenceEqual(removed.AsSpan(4 * index, 3)));
        byte[] isolated = Render(frame with { LastCommands = NativeBattleResolvedCommandList.From(frame.LastCommands.Where(IsParticle)) });
        int isolatedPixels = Enumerable.Range(0, 320 * 224).Count(index => isolated[4 * index] != 0 || isolated[4 * index + 1] != 0 || isolated[4 * index + 2] != 0);
        Console.WriteLine($"Private trail diagnostic: commands{particleCommands}, isolatedRGBpixels{isolatedPixels}, full-frameRGBdifference{changed}.");
        Assert.True(changed > 0, "Actual6C84C0 private packets must contribute visible pixels independently of common ribbons and GF skin.");
        Console.WriteLine($"Native particle visual control at frame{source.GetProperty("Frame").GetInt64()}/callback{callback}: before signature0, removed{particleCommands} private trail draws, changed{changed}RGB pixels.");

        byte[] Render(NativeBattlePrimitiveFrame value) => renderer.RenderNativePrimitiveFrame(value, memory.Textures, capabilities: definition.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess?.TextureWindowCache, effectTextureSource: assets.EffectTextureSource).ToArray();
    }

    private static bool IsParticle(NativeBattleLogicalResolvedCommand command)
    {
        //6C84C0/6C8620 fixed stripe UV/CLUT/page, shared with common ribbons but distinct by the actual effect-arena bank.
        ImmutableArray<uint> words = command.Command.Words;
        return command.Pass > 0 && command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0x3E && words.Length == 12 && words[2] == 0x3E94C008 && words[5] == 0x00B6C038 && (words[8] & 0xFFFF) == 0xC008 && (words[11] & 0xFFFF) == 0xC038;
    }
}
