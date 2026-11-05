using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class SegmentPlaybackPositionTests
{
    [Theory]
    [InlineData(1200, false)]
    [InlineData(17000, false)]
    [InlineData(40000, true)]
    [InlineData(68000, true)]
    public void Sample_clock_restore_matches_uninterrupted_sustaining_notes_tempo_and_loops(int frames, bool loop)
    {
        DlsBank bank = AudioFixtures.Bank();
        MusicSegment music = TempoSegment(loop);
        SegmentSynthesizer live = new(bank);
        live.Play(music, -300);
        Advance(live, frames);
        SegmentPlaybackPosition position = live.CapturePosition()!;
        Assert.Equal(frames, position.Frames);
        SegmentSynthesizer restored = new(bank);
        restored.PlayAtPosition(music, position, -300, TestContext.Current.CancellationToken);
        Assert.Equal(live.PositionSeconds, restored.PositionSeconds);
        double energy = 0;
        for (int frame = 0; frame < 1500; frame++)
        {
            live.RenderFrame(out double left, out double right);
            restored.RenderFrame(out double restoredLeft, out double restoredRight);
            Assert.Equal(left, restoredLeft);
            Assert.Equal(right, restoredRight);
            energy += Math.Abs(left) + Math.Abs(right);
        }
        Assert.True(energy > 0);
    }

    [Fact]
    public void Cancelled_preroll_preserves_the_current_live_source()
    {
        DlsBank bank = AudioFixtures.Bank();
        MusicSegment music = TempoSegment(true);
        SegmentSynthesizer source = new(bank);
        SegmentSynthesizer reference = new(bank);
        source.Play(music);
        reference.Play(music);
        Advance(source, 2200);
        Advance(reference, 2200);
        SegmentPlaybackPosition before = source.CapturePosition()!;
        Assert.Throws<OperationCanceledException>(() => source.PlayAtPosition(music, before with { Frames = 1000000 }, cancellationToken: new CancellationToken(true)));
        Assert.Equal(before, source.CapturePosition());
        for (int frame = 0; frame < 1000; frame++)
        {
            source.RenderFrame(out double left, out double right);
            reference.RenderFrame(out double referenceLeft, out double referenceRight);
            Assert.Equal(referenceLeft, left);
            Assert.Equal(referenceRight, right);
        }
    }

    [Fact]
    public void Finite_repeat_completion_and_release_tail_survive_sample_clock_restore()
    {
        DlsBank bank = AudioFixtures.Bank();
        MusicSegment music = AudioFixtures.Segment(repeats: 2, loopStart: 192, loopEnd: 384);
        SegmentSynthesizer live = new(bank);
        live.Play(music);
        Advance(live, 34000);
        Assert.False(live.IsPlaying);
        SegmentSynthesizer restored = new(bank);
        restored.PlayAtPosition(music, live.CapturePosition()!, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(restored.IsPlaying);
        for (int frame = 0; frame < 1000; frame++)
        {
            live.RenderFrame(out double left, out double right);
            restored.RenderFrame(out double restoredLeft, out double restoredRight);
            Assert.Equal(left, restoredLeft);
            Assert.Equal(right, restoredRight);
        }
    }

    [Fact]
    public void Audible_cursor_uses_output_history_across_paused_queue_frames()
    {
        SegmentSynthesizer source = new(AudioFixtures.Bank());
        source.Play(AudioFixtures.Segment());
        Advance(source, 100);
        source.Pause();
        Advance(source, 50);
        Assert.Equal(100, source.CapturePosition()!.Frames);
        SegmentPlaybackPosition audible = source.CapturePosition(60)!;
        Assert.Equal(90, audible.Frames);
        Assert.True(audible.Paused);
        SegmentSynthesizer restored = new(AudioFixtures.Bank());
        restored.PlayAtPosition(AudioFixtures.Segment(), audible, cancellationToken: TestContext.Current.CancellationToken);
        restored.RenderFrame(out double left, out double right);
        Assert.Equal(0, left);
        Assert.Equal(0, right);
        Assert.Equal(90, restored.CapturePosition()!.Frames);
        restored.Resume();
        restored.RenderFrame(out _, out _);
        Assert.Equal(91, restored.CapturePosition()!.Frames);
    }

    [Fact]
    public void Composition_restores_all_parts_on_the_same_advancing_clock()
    {
        DlsBank bank = AudioFixtures.Bank();
        MusicSegment[] music = [TempoSegment(true), AudioFixtures.Segment(repeats: uint.MaxValue, loopStart: 192, loopEnd: 384)];
        SegmentComposition live = new(bank);
        live.Play(music);
        Advance(live, 40000);
        SegmentComposition restored = new(bank);
        restored.PlayAtPosition(music, live.CapturePosition()!, cancellationToken: TestContext.Current.CancellationToken);
        for (int frame = 0; frame < 1000; frame++)
        {
            live.RenderFrame(out double left, out double right);
            restored.RenderFrame(out double restoredLeft, out double restoredRight);
            Assert.Equal(left, restoredLeft);
            Assert.Equal(right, restoredRight);
        }
    }

    [Fact]
    public void Restore_preserves_authenticated_starting_system_gain_and_rejects_a_different_clock()
    {
        DlsBank bank = AudioFixtures.Bank();
        SegmentSynthesizer live = new(bank);
        live.Play(AudioFixtures.SystemSegment([0xf0, 0x7f, 0x7f, 4, 1, 0, 0, 0xf7]));
        Advance(live, 1);
        live.Play(AudioFixtures.Segment());
        Advance(live, 100);
        SegmentPlaybackPosition position = live.CapturePosition()!;
        Assert.Equal(0, position.InitialSystemGain);
        SegmentSynthesizer restored = new(bank);
        restored.PlayAtPosition(AudioFixtures.Segment(), position, cancellationToken: TestContext.Current.CancellationToken);
        restored.RenderFrame(out double left, out double right);
        Assert.Equal(0, left);
        Assert.Equal(0, right);
        Assert.Throws<NotSupportedException>(() => new SegmentSynthesizer(bank, 22050).PlayAtPosition(AudioFixtures.Segment(), position, cancellationToken: TestContext.Current.CancellationToken));
    }

    private static void Advance(IAudioSource source, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            source.RenderFrame(out _, out _);
        }
    }

    private static MusicSegment TempoSegment(bool loop)
    {
        byte[] header = Data(writer =>
        {
            writer.Write(loop ? uint.MaxValue : 0u);
            writer.Write(3072);
            writer.Write(0);
            writer.Write(768);
            writer.Write(1536);
            writer.Write(0);
        });
        byte[] tempos = Data(writer =>
        {
            writer.Write(16);
            writer.Write(0);
            writer.Write(0);
            writer.Write(120d);
            writer.Write(768);
            writer.Write(0);
            writer.Write(180d);
        });
        byte[] notes = Data(writer =>
        {
            writer.Write(20);
            writer.Write(0);
            writer.Write(3072);
            writer.Write(0);
            writer.Write((short)0);
            writer.Write((byte)0x90);
            writer.Write((byte)60);
            writer.Write((byte)127);
            writer.Write(new byte[3]);
        });
        return new MusicSegment(AudioFixtures.Form("RIFF", "DMSG", AudioFixtures.Chunk("segh", header), AudioFixtures.Form("LIST", "trkl", Track("tetr", 0x85, AudioFixtures.Chunk("tetr", tempos)), Track("seqt", 0x86, AudioFixtures.Chunk("seqt", AudioFixtures.Chunk("evtl", notes))))));
    }

    private static byte[] Track(string name, int suffix, byte[] payload)
    {
        byte[] header = Data(writer =>
        {
            writer.Write(new Guid($"d2ac28{suffix:x2}-b39b-11d1-8704-00600893b1bd").ToByteArray());
            writer.Write(0);
            writer.Write(1);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(name));
            writer.Write(0);
        });
        return AudioFixtures.Form("RIFF", "DMTK", AudioFixtures.Chunk("trkh", header), payload);
    }
    private static byte[] Data(Action<BinaryWriter> write)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        write(writer);
        return stream.ToArray();
    }

}
