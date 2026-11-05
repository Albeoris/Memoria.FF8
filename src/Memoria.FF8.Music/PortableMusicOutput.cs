using Memoria.FF8.Audio;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8;

internal sealed class PortableMusicOutput : IDisposable
{
    private readonly SdlAudioOutput output;
    private readonly SegmentSynthesizer synthesizer;
    private readonly SegmentComposition concert;
    private readonly byte[] bankSha256;
    private string? contentSha256;
    private bool concertSelected;

    public PortableMusicOutput(string directory)
    {
        byte[] bankBytes = File.ReadAllBytes(Path.Combine(directory, "FF8.dls"));
        bankSha256 = SHA256.HashData(bankBytes);
        var bank = new DlsBank(bankBytes);
        output = new SdlAudioOutput();
        synthesizer = new SegmentSynthesizer(bank, output.Mixer.SampleRate);
        concert = new SegmentComposition(bank, output.Mixer.SampleRate);
        output.Mixer.Add(concert);
        output.Mixer.Add(synthesizer);
    }

    public bool IsPlaying
    {
        get
        {
            output.ThrowIfFailed();
            return synthesizer.IsPlaying || concert.IsPlaying;
        }
    }

    public void Play(string path, int volume, int startPoint = 0)
    {
        output.ThrowIfFailed();
        (MusicSegment[] segments, string fingerprint) = Load([path]);
        synthesizer.Play(segments[0], volume, startPoint);
        concert.Stop();
        contentSha256 = fingerprint;
        concertSelected = false;
    }

    public void PlayConcert(string[] paths, int volume)
    {
        output.ThrowIfFailed();
        (MusicSegment[] segments, string fingerprint) = Load(paths);
        concert.Play(segments, volume);
        synthesizer.Stop();
        contentSha256 = fingerprint;
        concertSelected = true;
    }

    public MusicPlaybackPosition? CapturePosition()
    {
        output.ThrowIfFailed();
        return output.CapturePlaybackPosition(queued =>
        {
            SegmentPlaybackPosition? position = concertSelected ? concert.CapturePosition(queued) : synthesizer.CapturePosition(queued);
            return contentSha256 is null || position is null ? null : new MusicPlaybackPosition(contentSha256, position);
        });
    }

    public void PlayAtPosition(string[] paths, MusicPlaybackPosition saved, int volume, bool isConcert, int fallbackStartPoint = 0)
    {
        output.ThrowIfFailed();
        (MusicSegment[] segments, string fingerprint) = Load(paths);
        bool compatible = string.Equals(saved.ContentSha256, fingerprint, StringComparison.Ordinal);
        try
        {
            if (saved.Position is null)
            {
                compatible = false;
            }
            else
            {
                saved.Position.Validate();
                compatible &= saved.Position.SampleRate == output.Mixer.SampleRate && segments.All(segment => saved.Position.StartPoint < segment.Length) && (!isConcert || saved.Position.InitialSystemGain == 1);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            compatible = false;
        }
        if (isConcert)
        {
            if (compatible && saved.Position is { } concertPosition)
            {
                using CancellationTokenSource budget = new(TimeSpan.FromSeconds(5));
                try
                {
                    concert.PlayAtPosition(segments, concertPosition, volume, budget.Token);
                }
                catch (OperationCanceledException) when (budget.IsCancellationRequested)
                {
                    concert.Play(segments, volume);
                }
            }
            else
            {
                concert.Play(segments, volume);
            }
            synthesizer.Stop();
        }
        else
        {
            if (segments.Length != 1)
            {
                throw new InvalidDataException("Single-segment playback requires one selected segment.");
            }
            if (compatible && saved.Position is { } segmentPosition)
            {
                using CancellationTokenSource budget = new(TimeSpan.FromSeconds(5));
                try
                {
                    synthesizer.PlayAtPosition(segments[0], segmentPosition, volume, budget.Token);
                }
                catch (OperationCanceledException) when (budget.IsCancellationRequested)
                {
                    synthesizer.Play(segments[0], volume, fallbackStartPoint);
                }
            }
            else
            {
                synthesizer.Play(segments[0], volume, fallbackStartPoint);
            }
            concert.Stop();
        }
        contentSha256 = fingerprint;
        concertSelected = isConcert;
    }

    private (MusicSegment[] Segments, string Fingerprint) Load(string[] paths)
    {
        if (paths.Length == 0)
        {
            throw new InvalidDataException("Music playback requires at least one segment.");
        }
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(SegmentPlaybackPosition.DecoderRevision));
        hash.AppendData(bankSha256);
        byte[] count = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(count, paths.Length);
        hash.AppendData(count);
        MusicSegment[] segments = new MusicSegment[paths.Length];
        for (int index = 0; index < paths.Length; index++)
        {
            byte[] bytes = File.ReadAllBytes(paths[index]);
            hash.AppendData(SHA256.HashData(bytes));
            segments[index] = new MusicSegment(bytes);
        }
        return (segments, Convert.ToHexString(hash.GetHashAndReset()));
    }

    public void Volume(int hundredthDecibels)
    {
        output.ThrowIfFailed();
        synthesizer.Volume(hundredthDecibels);
        concert.Volume(hundredthDecibels);
    }

    public void Pause()
    {
        synthesizer.Pause();
        concert.Pause();
    }

    public void Resume()
    {
        output.ThrowIfFailed();
        synthesizer.Resume();
        concert.Resume();
    }

    public void Stop()
    {
        contentSha256 = null;
        synthesizer.Stop();
        concert.Stop();
    }

    public void Dispose()
    {
        synthesizer.Stop();
        concert.Stop();
        output.Dispose();
    }
}
