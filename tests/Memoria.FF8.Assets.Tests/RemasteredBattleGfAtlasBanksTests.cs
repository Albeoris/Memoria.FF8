using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class RemasteredBattleGfAtlasBanksTests
{
    [Theory]
    [InlineData(184, 854, 256, 0, 384)]
    [InlineData(184, 864, 289, 1, 384)]
    [InlineData(184, 862, 256, 2, 384)]
    [InlineData(902, 865, 279, 0, 256)]
    [InlineData(902, 891, 302, 1, 256)]
    [InlineData(99, 704, 315, 0, 384)]
    [InlineData(99, 704, 328, 1, 384)]
    [InlineData(99, 711, 322, 2, 384)]
    [InlineData(95, 891, 403, 3, 512)]
    [InlineData(95, 886, 403, 0, 512)]
    [InlineData(95, 863, 386, 0, 512)]
    [InlineData(95, 870, 386, 1, 512)]
    [InlineData(95, 877, 386, 2, 512)]
    public void NativeSourceCopyCoordinatesSelectThePublishedBank(int identity, int x, int y, int bank, int canvasWidth)
    {
        NativeBattleTextureRegistration registration = Registration([new(checked((short)x), checked((short)y), 1, 1)]) with { CopyOrder = [0] };
        TexturePrimitiveSample sample = new(0.1f, identity == 95 ? 0.75f : 0.25f);
        TextureSourceAtlasAddress address = RemasteredBattleGfAtlasBanks.Apply(Address(), identity, identity == 99 ? 1 : 0, registration, sample);
        Assert.Null(address.UnsupportedReason);
        Assert.Equal(canvasWidth, address.CanvasWidth);
        Assert.Equal(bank * 128, address.SampleXOffset);
    }

    [Fact]
    public void LastMatchingObservationUsesRecencyInsteadOfTheStableFrameIndex()
    {
        NativeBattleTextureRegistration registration = Registration([new(854, 256, 1, 1), new(862, 256, 1, 1), new(850, 300, 1, 1)]) with { CopyOrder = [1, 0, 2] };
        TextureSourceAtlasAddress first = RemasteredBattleGfAtlasBanks.Apply(Address(), 184, 0, registration, new(0, 0));
        TextureSourceAtlasAddress second = RemasteredBattleGfAtlasBanks.Apply(Address(), 184, 0, registration with { CopyOrder = [0, 1, 2] }, new(0, 0));
        Assert.Equal(0, first.SampleXOffset);
        Assert.Equal(256, second.SampleXOffset);
    }

    [Fact]
    public void TheFirstPrimitiveSampleChoosesOneBankForTheWholeWindow()
    {
        NativeBattleTextureRegistration registration = Registration([new(862, 256, 1, 1)]) with { CopyOrder = [0] };
        TextureSourceAtlasAddress upper = RemasteredBattleGfAtlasBanks.Apply(Address(), 184, 0, registration, new(0, 0.49f));
        TextureSourceAtlasAddress lower = RemasteredBattleGfAtlasBanks.Apply(Address(), 184, 0, registration, new(0, 0.5f));
        Assert.Equal(256, upper.SampleXOffset);
        Assert.Equal(0, lower.SampleXOffset);
    }

    [Fact]
    public void HistoricalUnknownOrderAndMissingPrimitiveCoordinatesRemainExplicitlyUnsupported()
    {
        NativeBattleTextureRegistration unknown = Registration([new(862, 256, 1, 1)]);
        Assert.NotNull(RemasteredBattleGfAtlasBanks.Apply(Address(), 184, 0, unknown, new(0, 0)).UnsupportedReason);
        Assert.NotNull(RemasteredBattleGfAtlasBanks.Apply(Address(), 184, 0, unknown with { CopyOrder = [0] }).UnsupportedReason);
    }

    private static TextureSourceAtlasAddress Address() => new("mag777", 0, 2, 256, 256, 0, 0, 128, 128);

    private static NativeBattleTextureRegistration Registration(System.Collections.Immutable.ImmutableArray<NativeBattleEffectTextureRectangle> frames) => new(new("mag184.tim", 0, NativeBattleTextureRole.Pixels, 8, 64, 128), 832, 256, 64, 128, frames, frames.Length - 1) { CopyHistoryKnown = true, PaletteOwnedSpans = [] };
}
