using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBoundedVramTextureTests
{
    [Fact]
    public void Small_region_decoding_uses_current_palette_without_exporting_the_full_bank()
    {
        NativeBattleEffectTextureState previous = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [1, 0]).Upload45BD30(17, 0, 1, 1, [31, 0]);
        PixelImage oldPixels = NativeBattleEffectTextureReader.DecodeRegion(previous, 0, 1, 0, 0, 1, 1);
        NativeBattleEffectTextureState current = previous.Upload45BD30(17, 0, 1, 1, [0xE0, 3]);
        NativeBattleEffectTextureState queued = current.EnqueueRectangle505DF0(new(800, 400, 1, 1), new("pending-pixel", [2, 0]));
        Assert.Same(current.VramPublicationIdentity, queued.VramPublicationIdentity);
        long before = GC.GetAllocatedBytesForCurrentThread();
        PixelImage pixels = NativeBattleEffectTextureReader.DecodeRegion(queued, 0, 1, 0, 0, 1, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 131072, $"A bounded texture decode allocated {allocated} bytes.");
        Assert.Equal(Rgba32.FromPsxBgr555(31), oldPixels[0, 0]);
        Assert.Equal(Rgba32.FromPsxBgr555(0x03E0), pixels[0, 0]);
        Assert.False(NativeBattleEffectTextureReader.HasSamePcRegionSource(previous, current, 0, 1, 0, 0, 1, 1));
        Assert.True(NativeBattleEffectTextureReader.HasSamePcRegionSource(current, queued, 0, 1, 0, 0, 1, 1));
        Assert.Equal(Rgba32.FromPsxBgr555(31), NativeBattleEffectTextureReader.DecodeRegion(previous, 0, 1, 0, 0, 1, 1)[0, 0]);
    }
}
