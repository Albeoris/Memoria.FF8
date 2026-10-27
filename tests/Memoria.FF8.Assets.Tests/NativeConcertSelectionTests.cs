namespace Memoria.FF8.Assets.Tests;

public sealed class NativeConcertSelectionTests
{
    [Theory]
    [InlineData(0x2u, "043a-concert-tap.sgt")]
    [InlineData(0x4u, "043b-concert-flute.sgt")]
    [InlineData(0x8u, "043c-concert-fiddle.sgt")]
    [InlineData(0xFF0u, "043d-concert-aguitar.sgt")]
    [InlineData(0x1000u, "043e-concert-sax.sgt")]
    [InlineData(0x7E000u, "043f-concert-piano.sgt")]
    [InlineData(0x380000u, "043g-concert-eguitar.sgt")]
    [InlineData(0x400000u, "043h-concert-ebass.sgt")]
    public void ExactInstrumentGroupsSelectOnePreview(uint mask, string filename)
    {
        Assert.True(NativeConcertSelection.IsPreview(mask));
        Assert.Equal(filename, Assert.Single(NativeConcertSelection.Tracks(mask)));
    }

    [Fact]
    public void CombinedGroupsKeepNativeInstrumentOrderAndAllowAllEight()
    {
        Assert.False(NativeConcertSelection.IsPreview(0x03FFFFFF));
        Assert.Equal(new[]
        {
            "043a-concert-tap.sgt",
            "043b-concert-flute.sgt",
            "043c-concert-fiddle.sgt",
            "043d-concert-aguitar.sgt",
            "043e-concert-sax.sgt",
            "043f-concert-piano.sgt",
            "043g-concert-eguitar.sgt",
            "043h-concert-ebass.sgt"
        }, NativeConcertSelection.Tracks(0x03FFFFFF));
    }

    [Theory]
    [InlineData(0x10u)]
    [InlineData(0xFE0u)]
    [InlineData(0x2000u)]
    [InlineData(0x7C000u)]
    [InlineData(0x80000u)]
    [InlineData(0x180000u)]
    public void PartialMultiBitGroupsDoNotSelectAnInstrument(uint mask)
    {
        Assert.False(NativeConcertSelection.IsPreview(mask));
        Assert.Empty(NativeConcertSelection.Tracks(mask));
    }

    [Fact]
    public void CompletedAndPartialGroupsDoNotOverlapOrDuplicateTracks()
    {
        uint mask = 0x2 | 0x4 | 0xFE0 | 0x7E000 | 0x180000;
        Assert.False(NativeConcertSelection.IsPreview(mask));
        Assert.Equal(new[] { "043a-concert-tap.sgt", "043b-concert-flute.sgt", "043f-concert-piano.sgt" }, NativeConcertSelection.Tracks(mask));
    }

    [Fact]
    public void NonInstrumentBitsDoNotChangeTheSelectedGroups()
    {
        Assert.False(NativeConcertSelection.IsPreview(0x3));
        Assert.Equal(new[] { "043a-concert-tap.sgt" }, NativeConcertSelection.Tracks(0x3));
        Assert.Equal(new[] { "043h-concert-ebass.sgt" }, NativeConcertSelection.Tracks(0x400001));
    }

    [Fact]
    public void EmptyAndUnusedMasksSelectNoTracks()
    {
        Assert.False(NativeConcertSelection.IsPreview(0));
        Assert.Empty(NativeConcertSelection.Tracks(0));
        Assert.False(NativeConcertSelection.IsPreview(0x03800001));
        Assert.Empty(NativeConcertSelection.Tracks(0x03800001));
    }

    [Fact]
    public void ConditionalRuntimeMaskRemainsASeparateFourInstrumentSelection()
    {
        Assert.False(NativeConcertSelection.IsPreview(4095));
        Assert.Equal(new[] { "043a-concert-tap.sgt", "043b-concert-flute.sgt", "043c-concert-fiddle.sgt", "043d-concert-aguitar.sgt" }, NativeConcertSelection.Tracks(4095));
    }

    [Theory]
    [InlineData(0x04000000u)]
    [InlineData(uint.MaxValue)]
    public void UnexecutedUpperBitsAreRejected(uint mask)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeConcertSelection.Tracks(mask));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeConcertSelection.IsPreview(mask));
    }
}
