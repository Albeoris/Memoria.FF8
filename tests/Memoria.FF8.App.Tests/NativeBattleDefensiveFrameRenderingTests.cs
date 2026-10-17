using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDefensiveFrameRenderingTests
{
    [Theory]
    [InlineData(0, 48)]
    [InlineData(1, 24)]
    [InlineData(2, 60)]
    public void Original_defensive_packets_render_pixels_and_independent_frame_restores_match(byte variant, int polygons)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleDefensiveResource resource = game.ReadNativeBattleDefensiveResources().Single(item => item.Variant == variant);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096), 216, 160, 108);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Defensive, 0) { Phase = 2, Variant = variant, Scale = 4096, Anchor = new(0, 0, 0, 0) };
        NativeBattleModulePacketBatch batch = NativeBattleDefensivePackets.Emit570650(draw, resource, projection);
        Assert.Equal(polygons, batch.Primitives.Length);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        foreach (NativeBattleAllocatedPrimitive allocation in batch.Primitives)
        {
            NativeBattleGeometryPrimitive primitive = allocation.Primitive;
            ordering = ordering.Prepend(primitive.Bucket, new("off_B6D070", 0, allocation.ByteOffset), primitive.Words, allocation.Flags20, primitive.VertexPayload4, 0);
        }
        //This isolated frame supplies an explicit inherited additive page; no live battle history is reconstructed.
        var (_, commands, _) = NativeBattleLogicalPasses.Execute(ordering, assets.ReadNativePrimitiveTables(), new(0x20), 1);
        Assert.Contains(commands, command => command.Pass == 2);
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), commands, [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        string json = JsonSerializer.Serialize(frame);
        NativeBattlePrimitiveFrame first = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(json)!;
        NativeBattlePrimitiveFrame second = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(json)!;
        BattleRenderer firstRenderer = new(320, 216);
        BattleRenderer secondRenderer = new(320, 216);

        byte[] firstPixels = firstRenderer.RenderNativePrimitiveFrame(first, null).ToArray();
        byte[] secondPixels = secondRenderer.RenderNativePrimitiveFrame(second, null).ToArray();

        Assert.True(firstRenderer.DrawnTriangles > 0);
        Assert.Contains(Enumerable.Range(0, 320 * 216), index => firstPixels[index * 4] != 0 || firstPixels[index * 4 + 1] != 0 || firstPixels[index * 4 + 2] != 0);
        Assert.Equal(firstPixels, secondPixels);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }
}
