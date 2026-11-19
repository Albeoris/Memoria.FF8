using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.Audio;

/// <summary>Loads segments against one DLS bank and prepares an independent playback source.</summary>
public sealed class SegmentMusicBank(byte[] bankBytes)
{
    private readonly DlsBank bank = new(bankBytes);
    private readonly byte[] bankSha256 = SHA256.HashData(bankBytes);

    public async Task<SegmentMusicSource> PrepareAsync(string[] paths, int sampleRate, int volume, bool concert = false, int startPoint = 0, int previewMeasure = 0, MusicPlaybackPosition? saved = null, CancellationToken token = default)
    {
        if (paths.Length == 0 || !concert && paths.Length != 1)
        {
            throw new InvalidDataException("Music playback requires a single segment or a nonempty concert selection.");
        }
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(SegmentPlaybackPosition.DecoderRevision));
        hash.AppendData(bankSha256);
        byte[] count = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(count, paths.Length);
        hash.AppendData(count);
        MusicSegment[] segments = new MusicSegment[paths.Length];
        for (int index = 0; index < paths.Length; index++)
        {
            byte[] bytes = await File.ReadAllBytesAsync(paths[index], token).ConfigureAwait(false);
            hash.AppendData(SHA256.HashData(bytes));
            segments[index] = new MusicSegment(bytes);
        }
        string fingerprint = Convert.ToHexString(hash.GetHashAndReset());
        if (concert)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(previewMeasure);
            if (previewMeasure != 0 && segments.Length != 1)
            {
                throw new InvalidDataException("Instrument preview requires one selected segment.");
            }
            startPoint = previewMeasure == 0 ? 0 : segments[0].GetMeasureStart(previewMeasure);
        }

        SegmentSynthesizer? synthesizer = concert ? null : new(bank, sampleRate);
        SegmentComposition? composition = concert ? new(bank, sampleRate) : null;
        if (CompatiblePosition(saved, fingerprint, segments, sampleRate, concert, startPoint))
        {
            using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(token);
            budget.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                if (composition is not null)
                {
                    composition.PlayAtPosition(segments, saved!.Position, volume, budget.Token);
                }
                else
                {
                    synthesizer!.PlayAtPosition(segments[0], saved!.Position, volume, budget.Token);
                }
                return new SegmentMusicSource(fingerprint, synthesizer, composition);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && budget.IsCancellationRequested)
            {
                // Restart presentation if restoring the sample clock exceeds its budget.
            }
        }
        token.ThrowIfCancellationRequested();
        if (composition is not null)
        {
            composition.Play(segments, volume, startPoint);
        }
        else
        {
            synthesizer!.Play(segments[0], volume, startPoint);
        }
        return new SegmentMusicSource(fingerprint, synthesizer, composition);
    }

    private static bool CompatiblePosition(MusicPlaybackPosition? saved, string fingerprint, MusicSegment[] segments, int sampleRate, bool concert, int startPoint)
    {
        if (saved?.Position is not { } position || !string.Equals(saved.ContentSha256, fingerprint, StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            position.Validate();
        }
        catch (InvalidDataException)
        {
            return false;
        }
        return position.SampleRate == sampleRate && segments.All(segment => position.StartPoint < segment.Length) && (!concert || position.InitialSystemGain == 1 && position.StartPoint == startPoint);
    }
}

public sealed class SegmentMusicSource : IAudioSource
{
    private readonly string fingerprint;
    private readonly SegmentSynthesizer? synthesizer;
    private readonly SegmentComposition? composition;
    private readonly IAudioSource source;
    private Exception? error;

    internal SegmentMusicSource(string fingerprint, SegmentSynthesizer? synthesizer, SegmentComposition? composition)
    {
        this.fingerprint = fingerprint;
        this.synthesizer = synthesizer;
        this.composition = composition;
        source = (IAudioSource?)composition ?? synthesizer!;
    }

    public Exception? Error => Volatile.Read(ref error);
    public bool IsPlaying => Error is null && (composition?.IsPlaying ?? synthesizer!.IsPlaying);

    public MusicPlaybackPosition? CapturePosition(int queuedFrames = 0)
    {
        if (Error is not null)
        {
            return null;
        }
        SegmentPlaybackPosition? position = composition is not null ? composition.CapturePosition(queuedFrames) : synthesizer!.CapturePosition(queuedFrames);
        return position is null ? null : new MusicPlaybackPosition(fingerprint, position);
    }

    public void Volume(int gain)
    {
        if (composition is not null)
        {
            composition.Volume(gain);
        }
        else
        {
            synthesizer!.Volume(gain);
        }
    }

    public void Pause()
    {
        if (composition is not null)
        {
            composition.Pause();
        }
        else
        {
            synthesizer!.Pause();
        }
    }

    public void Resume()
    {
        if (composition is not null)
        {
            composition.Resume();
        }
        else
        {
            synthesizer!.Resume();
        }
    }

    public void RenderFrame(out double left, out double right)
    {
        if (Error is null)
        {
            try
            {
                source.RenderFrame(out left, out right);
                return;
            }
            catch (Exception exception) when (exception is NotSupportedException or ArgumentException or InvalidDataException or InvalidOperationException or IndexOutOfRangeException or OverflowException)
            {
                Volatile.Write(ref error, exception);
            }
        }
        left = 0;
        right = 0;
    }
}
