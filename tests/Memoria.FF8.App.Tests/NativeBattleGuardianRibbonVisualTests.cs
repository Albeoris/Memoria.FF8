using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianRibbonVisualTests
{
    [Fact]
    public void Named_completed_GF_ribbon_frame_changes_actual_pixels_when_only_its_common_GT4_packets_are_removed()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_RIBBON_VISUAL_SNAPSHOT");
        Assert.SkipUnless(path is not null, "A concrete completed native GF ribbon frame is required.");
        (JsonElement source, long callback, NativeBattlePrimitiveFrame frame) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(path!, 116);
        bool Ribbon(NativeBattleLogicalResolvedCommand command) => command.Command.Packet.Bank is "off_B6D070" or "off_B6D094" && (command.Command.Opcode & 0xFC) == 0x3C;
        int ribbonCommands = frame.LastCommands.Count(command => command.Pass > 0 && Ribbon(command));
        Assert.True(ribbonCommands > 0);
        NativeBattlePrimitiveFrame without = frame with { LastCommands = NativeBattleResolvedCommandList.From(frame.LastCommands.Where(command => !Ribbon(command))) };
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleGraphicsDefinition definition = Assert.IsType<NativeBattleGraphicsDefinition>(NativeBattleFixtureState.ReadArchived<NativeBattleGraphicsDefinition>(source.GetProperty("Definition"), "NativeGraphics"));
        NativeBattleEffectMemory memory = Assert.IsType<NativeBattleEffectMemory>(NativeBattleFixtureState.ReadArchived<NativeBattleEffectMemory>(source, "NativeEffectMemory"));
        BattleRenderer renderer = new(320, 224, ViewportLayout.Fit(320, 240, 320, 224));
        byte[] full = Render(frame);
        byte[] removed = Render(without);
        int changed = Enumerable.Range(0, 320 * 224).Count(index => !full.AsSpan(4 * index, 3).SequenceEqual(removed.AsSpan(4 * index, 3)));
        Assert.True(changed > 0, "The reached native ribbon packets must make a measurable visible contribution in the named frame.");
        Console.WriteLine($"Native ribbon visual control at frame{source.GetProperty("Frame").GetInt64()}/callback{callback}: removed{ribbonCommands} resolved common GT4 draws, changed{changed} RGB pixels.");

        byte[] Render(NativeBattlePrimitiveFrame value) => renderer.RenderNativePrimitiveFrame(value, memory.Textures, capabilities: definition.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess?.TextureWindowCache, effectTextureSource: assets.EffectTextureSource).ToArray();
    }
}
