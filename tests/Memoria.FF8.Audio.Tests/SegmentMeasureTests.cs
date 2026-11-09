using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class SegmentMeasureTests
{
    [Fact]
    public void Missing_time_signature_uses_four_quarter_notes_per_measure()
    {
        MusicSegment segment = AudioFixtures.Segment();
        Assert.Equal(0, segment.GetMeasureStart(0));
        Assert.Equal(18432, segment.GetMeasureStart(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetMeasureStart(-1));
        Assert.Throws<OverflowException>(() => segment.GetMeasureStart(int.MaxValue));
    }

    [Fact]
    public void Measure_conversion_uses_beat_units_and_signature_changes()
    {
        MusicSegment segment = AudioFixtures.Segment(timeSignatures: [(0, 3, 4), (4608, 6, 8)]);
        Assert.Equal(2304, segment.GetMeasureStart(1));
        Assert.Equal(4608, segment.GetMeasureStart(2));
        Assert.Equal(6912, segment.GetMeasureStart(3));
    }

    [Fact]
    public void Missing_initial_signature_defaults_until_the_first_change()
    {
        MusicSegment segment = AudioFixtures.Segment(timeSignatures: [(6144, 3, 4)]);
        Assert.Equal(3072, segment.GetMeasureStart(1));
        Assert.Equal(8448, segment.GetMeasureStart(3));
    }

    [Fact]
    public void Conversion_refuses_unproved_partial_measure_boundaries()
    {
        MusicSegment segment = AudioFixtures.Segment(timeSignatures: [(0, 4, 4), (1000, 3, 4)]);
        Assert.Equal(0, segment.GetMeasureStart(0));
        Assert.Throws<NotSupportedException>(() => segment.GetMeasureStart(1));
    }

    [Theory]
    [InlineData(0, 4, 4)]
    [InlineData(4, 0, 4)]
    [InlineData(4, 3, 4)]
    [InlineData(4, 4, -1)]
    public void Invalid_time_signatures_are_rejected(int beats, int unit, int tick)
    {
        Assert.Throws<InvalidDataException>(() => AudioFixtures.Segment(timeSignatures: [(tick, (byte)beats, (byte)unit)]));
    }

    [Theory]
    [InlineData("043a-concert-tap.sgt", 0, 0)]
    [InlineData("043b-concert-flute.sgt", 6, 18432)]
    [InlineData("043c-concert-fiddle.sgt", 6, 18432)]
    [InlineData("043d-concert-aguitar.sgt", 2, 6144)]
    [InlineData("043e-concert-sax.sgt", 4, 12288)]
    [InlineData("043f-concert-piano.sgt", 0, 0)]
    [InlineData("043g-concert-eguitar.sgt", 0, 0)]
    [InlineData("043h-concert-ebass.sgt", 0, 0)]
    public void Installed_instrument_previews_render_independently_from_their_selected_measure(string filename, int measure, int expectedTick)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "An explicitly configured original music installation is required.");
        string directory = Path.Combine(game!, "Data", "Music", "dmusic");
        MusicSegment segment = new(File.ReadAllBytes(Path.Combine(directory, filename)));
        Assert.Equal(expectedTick, segment.GetMeasureStart(measure));
        DlsBank bank = new(File.ReadAllBytes(Path.Combine(directory, "FF8.dls")));
        SegmentComposition composition = new(bank);
        composition.Play([segment], startPoint: expectedTick);
        double energy = 0;
        for (int frame = 0; frame < 88200; frame++)
        {
            composition.RenderFrame(out double left, out double right);
            Assert.True(double.IsFinite(left) && double.IsFinite(right));
            energy += Math.Abs(left) + Math.Abs(right);
        }
        Assert.True(energy > 0, filename);
        Assert.Equal(expectedTick, composition.CapturePosition()!.StartPoint);
    }
}
