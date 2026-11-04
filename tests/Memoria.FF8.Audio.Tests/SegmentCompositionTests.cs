using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class SegmentCompositionTests
{
    [Fact]
    public void Installed_concert_instruments_decode_and_render_as_a_composition()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "An explicitly configured original music installation is required.");
        string directory = Path.Combine(game!, "Data", "Music", "dmusic");
        DlsBank bank = new(File.ReadAllBytes(Path.Combine(directory, "FF8.dls")));
        string[] filenames = ["043a-concert-tap.sgt", "043b-concert-flute.sgt", "043c-concert-fiddle.sgt", "043d-concert-aguitar.sgt", "043e-concert-sax.sgt", "043f-concert-piano.sgt", "043g-concert-eguitar.sgt", "043h-concert-ebass.sgt"];
        MusicSegment[] segments = filenames.Select(name => new MusicSegment(File.ReadAllBytes(Path.Combine(directory, name)))).ToArray();
        foreach (MusicSegment segment in segments)
        {
            Assert.Empty(new SegmentSynthesizer(bank).InspectInstrumentCoverage(segment).MissingInstruments);
        }
        SegmentComposition composition = new(bank);
        composition.Play(segments);
        double energy = 0;
        for (int frame = 0; frame < 44100; frame++)
        {
            composition.RenderFrame(out double left, out double right);
            Assert.True(double.IsFinite(left) && double.IsFinite(right));
            energy += Math.Abs(left) + Math.Abs(right);
        }
        Assert.True(energy > 0);
        Assert.True(composition.IsPlaying);
    }

    [Fact]
    public void Parts_share_a_clock_without_sharing_instrument_channels_and_pause_together()
    {
        DlsBank bank = AudioFixtures.Bank();
        MusicSegment segment = AudioFixtures.Segment();
        SegmentComposition single = new(bank);
        SegmentComposition duet = new(bank);
        single.Play([segment]);
        duet.Play([segment, segment]);
        double energy = 0;
        for (int frame = 0; frame < 1000; frame++)
        {
            single.RenderFrame(out double left, out double right);
            duet.RenderFrame(out double mixedLeft, out double mixedRight);
            Assert.Equal(left * 2, mixedLeft);
            Assert.Equal(right * 2, mixedRight);
            energy += Math.Abs(left) + Math.Abs(right);
        }
        Assert.True(energy > 0);
        duet.Pause();
        Assert.False(duet.IsPlaying);
        duet.RenderFrame(out double pausedLeft, out double pausedRight);
        Assert.Equal(0, pausedLeft);
        Assert.Equal(0, pausedRight);
        duet.Resume();
        single.RenderFrame(out double resumedLeft, out double resumedRight);
        duet.RenderFrame(out double duetLeft, out double duetRight);
        Assert.Equal(resumedLeft * 2, duetLeft);
        Assert.Equal(resumedRight * 2, duetRight);
        duet.Stop();
        Assert.False(duet.IsPlaying);
        duet.Play([]);
        Assert.False(duet.IsPlaying);
    }

    [Fact]
    public void Invalid_new_selection_preserves_the_previous_composition()
    {
        SegmentComposition composition = new(AudioFixtures.Bank());
        MusicSegment segment = AudioFixtures.Segment();
        composition.Play([segment]);
        Assert.Throws<ArgumentOutOfRangeException>(() => composition.Play([segment], startPoint: segment.Length));
        Assert.True(composition.IsPlaying);
    }
}
