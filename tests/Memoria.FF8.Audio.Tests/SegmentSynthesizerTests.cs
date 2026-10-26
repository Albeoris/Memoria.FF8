using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class SegmentSynthesizerTests
{
    [Fact]
    public void UnknownEssentialTrackClassAndPayloadAreRejected()
    {
        byte[] header = new byte[32];
        System.Text.Encoding.ASCII.GetBytes("seqt").CopyTo(header, 24);
        byte[] malformed = AudioFixtures.Form("RIFF", "DMSG", AudioFixtures.Form("LIST", "trkl", AudioFixtures.Form("RIFF", "DMTK", AudioFixtures.Chunk("trkh", header), AudioFixtures.Chunk("seqt", []))));
        Assert.Throws<NotSupportedException>(() => new MusicSegment(malformed));
        new Guid("d2ac2886-b39b-11d1-8704-00600893b1bd").ToByteArray().CopyTo(header, 0);
        malformed = AudioFixtures.Form("RIFF", "DMSG", AudioFixtures.Form("LIST", "trkl", AudioFixtures.Form("RIFF", "DMTK", AudioFixtures.Chunk("trkh", header), AudioFixtures.Chunk("seqt", []), AudioFixtures.Chunk("NEWW", []))));
        Assert.Throws<NotSupportedException>(() => new MusicSegment(malformed));
    }

    [Fact]
    public void ChannelTenDefaultsToTheGmPercussionBankWithoutAPatchEvent()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank(drum: true));
        MusicSegment segment = AudioFixtures.Segment(channel: 9);
        SegmentInstrumentCoverage coverage = synth.InspectInstrumentCoverage(segment);
        Assert.Empty(coverage.MissingInstruments);
        Assert.Equal(1, coverage.MatchedRegions);
        synth.Play(segment);
        Assert.True(Render(synth, 1000) > 0);
    }

    [Fact]
    public void UniversalMasterVolumePersistsUntilAnExecutedSystemReset()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank());
        synth.Play(AudioFixtures.SystemSegment([0xf0, 0x7f, 0x7f, 4, 1, 0, 0, 0xf7]));
        Render(synth, 1);
        synth.Play(AudioFixtures.Segment());
        Assert.Equal(0, Render(synth, 1000));
        synth.Play(AudioFixtures.SystemSegment([0xf0, 0x43, 0x10, 0x4c, 0, 0, 0x7e, 0, 0xf7]));
        Render(synth, 1);
        synth.Play(AudioFixtures.Segment());
        Assert.True(Render(synth, 1000) > 0);
    }

    [Fact]
    public void UnimplementedHardwareEffectRequestsFailExplicitly()
    {
        Assert.Throws<NotSupportedException>(() => AudioFixtures.SystemSegment([0xf0, 0x43, 0x10, 0x4c, 2, 1, 0, 1, 0, 0xf7]));
    }

    [Fact]
    public void DlsLevelOneUnqualifiedVelocityAndDuplicateLocaleUseCollectionOrder()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank(velocityLow: 0, velocityHigh: 0, duplicateLocale: true));
        MusicSegment segment = AudioFixtures.Segment();
        SegmentInstrumentCoverage coverage = synth.InspectInstrumentCoverage(segment);
        Assert.Equal(1, coverage.Notes);
        Assert.Equal(1, coverage.MatchedRegions);
        Assert.Empty(coverage.UnmappedNotes);
        synth.Play(segment);
        Assert.True(Render(synth, 1000) > 0);
    }

    [Fact]
    public void AnExistingInstrumentWithoutAMatchingRegionIsSilentAndReported()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank(velocityLow: 1, velocityHigh: 1));
        MusicSegment segment = AudioFixtures.Segment();
        SegmentInstrumentCoverage coverage = synth.InspectInstrumentCoverage(segment);
        Assert.Empty(coverage.MissingInstruments);
        Assert.Single(coverage.UnmappedNotes);
        synth.Play(segment);
        Assert.Equal(0, Render(synth, 1000));
    }

    [Fact]
    public void SharedReverbTailDoesNotDelayMusicalCompletion()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank());
        synth.Play(AudioFixtures.Segment());
        Render(synth, 22052);
        Assert.False(synth.IsPlaying);
        Assert.True(Render(synth, 4410) > 0);
    }

    [Fact]
    public void OriginalSampleLoopSustainsUntilSegmentCompletion()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank());
        synth.Play(AudioFixtures.Segment());
        double energy = Render(synth, 22050);
        Assert.True(energy > 1000000);
        Render(synth, 2);
        Assert.False(synth.IsPlaying);
    }

    [Fact]
    public void SegmentLoopUsesMusicalBoundsAndFiniteRepeatCount()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank());
        synth.Play(AudioFixtures.Segment(repeats: 1, loopStart: 0, loopEnd: 384));
        Render(synth, 22050);
        Assert.True(synth.IsPlaying);
        Assert.InRange(synth.PositionSeconds, 0.249, 0.251);
        Render(synth, 11027);
        Assert.False(synth.IsPlaying);
    }

    [Fact]
    public void StartPointPauseAndResumeUseRetainedSampleClock()
    {
        var synth = new SegmentSynthesizer(AudioFixtures.Bank());
        synth.Play(AudioFixtures.Segment(), startPoint: 384);
        Assert.Equal(0.25, synth.PositionSeconds, 6);
        synth.Pause();
        Assert.Equal(0, Render(synth, 22050));
        Assert.Equal(0.25, synth.PositionSeconds, 6);
        synth.Resume();
        Render(synth, 11027);
        Assert.False(synth.IsPlaying);
    }

    [Fact]
    public void MasterGainUsesHundredthDecibelsAndMuteRetainsClock()
    {
        DlsBank bank = AudioFixtures.Bank();
        MusicSegment segment = AudioFixtures.Segment();
        var first = new SegmentSynthesizer(bank);
        var second = new SegmentSynthesizer(bank);
        first.Play(segment);
        second.Play(segment, -2000);
        double loud = Render(first, 100);
        double quiet = Render(second, 100);
        Assert.Equal(0.1, quiet / loud, 8);
        second.Volume(-10000);
        Assert.Equal(0, Render(second, 100));
        Assert.True(second.PositionSeconds > first.PositionSeconds);
    }

    [Fact]
    public void MalformedRiffAndSampleRangesFailBeforePlayback()
    {
        Assert.Throws<InvalidDataException>(() => RiffChunk.Read("RIFF\u007f\u007f\u007f\u007fDMSG"u8.ToArray(), "DMSG"));
        Assert.Throws<InvalidDataException>(() => AudioFixtures.Segment(loopEnd: 769));
    }

    private static double Render(SegmentSynthesizer synth, int frames)
    {
        double energy = 0;
        for (int i = 0; i < frames; i++)
        {
            synth.RenderFrame(out double left, out double right);
            Assert.True(double.IsFinite(left) && double.IsFinite(right));
            energy += Math.Abs(left) + Math.Abs(right);
        }
        return energy;
    }
}
