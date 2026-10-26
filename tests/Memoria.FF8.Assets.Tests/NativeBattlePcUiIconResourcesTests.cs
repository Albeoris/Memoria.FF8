using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePcUiIconResourcesTests
{
    [Fact]
    public void Original_countdown_digits_keep_authentic_parts_palettes_and_page_extents()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        NativeBattlePcUiIconResources resources = NativeBattlePcUiIconResources.ReadOrdinary(new ResourceService(source));
        NativeBattlePcUiBinding binding = resources.Binding;
        NativeBattlePcUiImmediateState state = NativeBattlePcUiImmediateState.FromColdProcess().Initialize4972A0(binding.Device).BeginFrame49B120(binding.Rules);
        for (byte icon = 0x18; icon <= 0x21; icon++)
        {
            state = state.DrawString4A93D0([icon, 0], 0, 0xFFFFFF, binding.IconCount, binding.Encode, binding.Opaque, binding.Semi, binding.Rules);
            state = state.DrawString4A93D0([icon, 0], 0, 0x04FFFFFF, binding.IconCount, binding.Encode, binding.Opaque, binding.Semi, binding.Rules);
        }
        Assert.True(state.Opaque.Length >= 10);
        Assert.Equal(state.Opaque.Length, state.CurrentSemi.Length);
        foreach (NativeBattlePcUiQuad quad in state.Opaque)
        {
            Assert.Equal(0, quad.Page);
            Assert.All(quad.Vertices, vertex => Assert.InRange(vertex.U, 0, 1));
            Assert.All(quad.Vertices, vertex => Assert.InRange(vertex.V, 0, 1));
            Assert.Contains(resources.Resolve(quad.Page, quad.Palette, false).Pixels, pixel => pixel.A != 0);
        }
    }

    [Fact]
    public void Semi_palette_mutation_preserves_zero_and_sets_every_nonzero_alpha_to127()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        NativeBattlePcUiIconResources resources = NativeBattlePcUiIconResources.ReadOrdinary(new ResourceService(source));
        NativeBattlePcUiBinding binding = resources.Binding;
        NativeBattlePcUiImmediateState state = NativeBattlePcUiImmediateState.FromColdProcess().Initialize4972A0(binding.Device).BeginFrame49B120(binding.Rules);
        state = state.DrawString4A93D0([0x18, 0], 0, 0xFFFFFF, binding.IconCount, binding.Encode, binding.Opaque, binding.Semi, binding.Rules);
        int palette = state.Opaque[0].Palette;
        var opaque = resources.Resolve(0, palette, false);
        var semi = resources.Resolve(0, palette, true);
        Assert.Same(semi, resources.Resolve(0, palette, true));
        for (int index = 0; index < opaque.Pixels.Length; index++)
        {
            Assert.Equal(opaque.Pixels[index] with { A = opaque.Pixels[index].A == 0 ? (byte)0 : (byte)127 }, semi.Pixels[index]);
        }
    }
}
