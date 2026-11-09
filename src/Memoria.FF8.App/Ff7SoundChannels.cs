using System.Buffers.Binary;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

/// <summary>Explicit native resident channels and the independent global streamed SFX owner.</summary>
internal sealed class Ff7SoundChannels(Func<double>? elapsedSeconds = null) : IDisposable
{
    private readonly Dictionary<int, Voice> voices = [];
    private double tempo = 1;

    public static (int Start, int End) ConvertLoopBounds(Ff7SoundSample sample, int outputRate, int outputChannels)
    {
        ReadOnlySpan<byte> format = RiffChunk.Read(sample.WaveBytes, "WAVE").Required("fmt ").Data.Span;
        int tag = BinaryPrimitives.ReadUInt16LittleEndian(format);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
        int rate = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(format[4..]));
        int bits = tag == 2 ? 16 : BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
        if (rate <= 0 || channels is not (1 or 2) || bits is not (8 or 16) || outputRate <= 0 || outputChannels is not (1 or 2))
        {
            throw new InvalidDataException("FFVII loop conversion requires a supported native PCM frame width and output format.");
        }
        int nativeFrameBytes = channels * bits / 8;
        if (sample.LoopStartPcmByte % nativeFrameBytes != 0 || sample.LoopEndPcmByte % nativeFrameBytes != 0)
        {
            throw new InvalidDataException("FFVII native loop bounds split an original PCM frame.");
        }
        int Convert(uint position) => checked((int)(position / nativeFrameBytes * (long)outputRate / rate * outputChannels * sizeof(short)));
        return (Convert(sample.LoopStartPcmByte), Convert(sample.LoopEndPcmByte));
    }

    public void Play(PcmAudioOutput output, byte[] pcm, int rate, int channels, bool stream, int pan, int gain, int sampleIndex = -1, bool loop = false, int loopStart = 0, int loopEnd = 0, string fingerprint = "", int channel = 1, Ff7SoundVoicePosition? resume = null, bool stopStream = true, int nativeVolume = 127, int nativeSampleRate = 0, int nativePitch = 0, double? nativeLoopDelaySeconds = null, long nativePanVersion = 0, long nativeFrequencyVersion = 0)
    {
        if (channels is not (1 or 2) || rate <= 0 || channel is < 1 or > 5 || nativeVolume is < 0 or > 127)
        {
            throw new InvalidDataException("FFVII sound requires a supported PCM format and native channel.");
        }
        int alignment = channels * sizeof(short);
        if (loop && (stream || loopStart < 0 || loopEnd <= loopStart || loopEnd > pcm.Length || loopStart % alignment != 0 || loopEnd % alignment != 0))
        {
            throw new InvalidDataException("FFVII resident loop requires aligned decoded PCM start/end bounds.");
        }
        if (resume is null && (stopStream || stream))
        {
            Stop(0);
        }
        if (resume is null && stream)
        {
            Stop(2);
            if (!stopStream)
            {
                Stop(channel);
            }
        }
        int owner = stream ? 0 : channel;
        if (resume is null && !stream && loop && sampleIndex >= 0 && voices.TryGetValue(owner, out Voice? active) && active.Sample == sampleIndex && active.Loop && active.IsPlaying)
        {
            return;
        }
        Stop(owner);
        Voice voice = new(output, pcm, rate, channels, pan, gain, sampleIndex, loop, loopStart, loopEnd, fingerprint, resume, nativeVolume, nativeSampleRate == 0 ? rate : nativeSampleRate, nativePitch, nativeLoopDelaySeconds, elapsedSeconds, nativePanVersion, nativeFrequencyVersion);
        voice.SetTempo(tempo);
        voices.Add(owner, voice);
    }

    public Ff7SoundVoicePosition[] Capture(int queuedFrames)
    {
        return voices.OrderBy(pair => pair.Key).Select(pair => pair.Value.Capture(pair.Key, queuedFrames)).Where(position => position is not null).Select(position => position!).ToArray();
    }

    public bool RestoreVoice(PcmAudioOutput output, byte[] pcm, int rate, int channels, int sample, bool stream, bool loop, int start, int end, string fingerprint, Ff7SoundVoicePosition saved, int nativeSampleRate = 0, double? nativeLoopDelaySeconds = null)
    {
        int alignment = channels * sizeof(short);
        int terminal = (loop ? end : pcm.Length) / alignment;
        if (saved.SoundId != sample || !saved.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase) || saved.SampleRate != rate || saved.Channels != channels || saved.Loop != loop || saved.LoopStartFrames != (loop ? start / alignment : 0) || saved.LoopEndFrames != terminal || saved.PositionFrames < 0 || (saved.PositionFrames > terminal || saved.PositionFrames == terminal && saved.LoopHandoffRemainingSeconds is null) || saved.NativeSampleRate != 0 && saved.NativeSampleRate != (nativeSampleRate == 0 ? rate : nativeSampleRate) || saved.LoopStarted && (!loop || start == 0 || saved.PositionFrames < start / alignment) || stream != (saved.Channel == 0))
        {
            return false;
        }
        Play(output, pcm, rate, channels, stream, saved.Pan, saved.Gain, sample, loop, start, end, fingerprint, saved.Channel == 0 ? 1 : saved.Channel, saved, nativeVolume: saved.NativeVolume, nativeSampleRate: nativeSampleRate, nativeLoopDelaySeconds: nativeLoopDelaySeconds);
        return true;
    }

    public void RetireFinished(bool paused, double? loopElapsedSeconds = null)
    {
        foreach (var pair in voices.ToArray())
        {
            pair.Value.Update(loopElapsedSeconds);
            if (!paused && !pair.Value.Loop && !pair.Value.IsPlaying)
            {
                Stop(pair.Key);
            }
        }
    }

    public void Pause(bool paused)
    {
        foreach (Voice voice in voices.Values)
        {
            voice.Pause(paused);
        }
    }

    public void SetTempo(double speed)
    {
        if (!double.IsFinite(speed) || speed is < 0.5 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }
        if (tempo == speed)
        {
            return;
        }
        tempo = speed;
        foreach (Voice voice in voices.Values)
        {
            voice.SetTempo(speed);
        }
    }

    public static Ff7SoundVoicePosition[] FilterStoppedResidentPositions(Ff7SoundVoicePosition[] positions, bool[] stopped)
    {
        return positions.Where(voice => voice.Channel == 0 || !stopped[voice.Channel - 1]).ToArray();
    }

    public void StopResidentChannel(int channel)
    {
        if (channel is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }
        Stop(channel);
    }

    public void StopNativeChannel(int channel)
    {
        if (channel is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }
        Stop(channel);
        Stop(0);
    }

    public double NextHandoffSeconds()
    {
        return voices.Values.Select(voice => voice.HandoffRemainingSeconds).DefaultIfEmpty(double.PositiveInfinity).Min();
    }

    public bool IsLoopPlaying(int channel, int sample)
    {
        return voices.TryGetValue(channel, out Voice? voice) && voice.Sample == sample && voice.Loop && voice.IsPlaying;
    }

    public void SetNativeFrequency(int channel, int frequency)
    {
        if (!Ff7NativeSoundFrequency.Accepted(frequency))
        {
            return;
        }
        if (voices.TryGetValue(channel, out Voice? resident))
        {
            resident.SetPrimaryFrequency(frequency);
        }
        if (channel == 1 && voices.TryGetValue(0, out Voice? stream))
        {
            stream.SetPrimaryFrequency(frequency);
        }
    }

    public void SetGain(int channel, int gain, int? nativeVolume = null)
    {
        if (channel is < 1 or > 5 || nativeVolume is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }
        if (voices.TryGetValue(channel, out Voice? voice))
        {
            voice.SetGain(gain, nativeVolume ?? voice.NativeVolume);
        }
        if (channel == 1 && voices.TryGetValue(0, out Voice? stream))
        {
            stream.SetGain(gain, nativeVolume ?? stream.NativeVolume);
        }
    }

    public void RefreshFrequencies(IReadOnlyList<int> pitches, IReadOnlyList<int> baseFrequencies, IReadOnlyList<long> versions)
    {
        if (pitches.Count != 5 || baseFrequencies.Count != 5 || versions.Count != 5 || pitches.Any(pitch => pitch is < -128 or > 127) || baseFrequencies.Any(rate => rate <= 0) || versions.Any(version => version < 0))
        {
            throw new InvalidDataException("FFVII frequency publication requires bounded native channel state.");
        }
        foreach (var pair in voices)
        {
            int index = pair.Key == 0 ? 0 : pair.Key - 1;
            pair.Value.PublishFrequency(Ff7NativeSoundFrequency.Frequency(baseFrequencies[index], pitches[index]), versions[index]);
        }
    }

    public void RefreshPans(IReadOnlyList<int> pans, IReadOnlyList<long> versions, IReadOnlyList<int> table)
    {
        if (pans.Count != 5 || versions.Count != 5 || table.Count != 128 || pans.Any(pan => pan is < 0 or > 127) || versions.Any(version => version < 0))
        {
            throw new InvalidDataException("FFVII pan publication requires bounded native channel state.");
        }
        foreach (var pair in voices)
        {
            int index = pair.Key == 0 ? 0 : pair.Key - 1;
            pair.Value.PublishPan(table[pans[index]], versions[index]);
        }
    }

    public void RefreshGains(IReadOnlyList<int> residentVolumes, Func<int, int> outputGain)
    {
        ArgumentNullException.ThrowIfNull(residentVolumes);
        ArgumentNullException.ThrowIfNull(outputGain);
        if (residentVolumes.Count != 5 || residentVolumes.Any(volume => volume is < 0 or > 127))
        {
            throw new InvalidDataException("FFVII live gain requires its five native channel intensities.");
        }
        foreach (var pair in voices)
        {
            int volume = pair.Key == 0 ? pair.Value.NativeVolume : residentVolumes[pair.Key - 1];
            pair.Value.SetGain(outputGain(volume), volume);
        }
    }

    public void StopAll()
    {
        foreach (int channel in voices.Keys.ToArray())
        {
            Stop(channel);
        }
    }

    public void Dispose()
    {
        StopAll();
    }

    private void Stop(int channel)
    {
        if (voices.Remove(channel, out Voice? voice))
        {
            voice.Dispose();
        }
    }

    private sealed class Voice : IDisposable
    {
        private readonly PcmAudioOutput.Buffer primary;
        private readonly PcmAudioOutput.Buffer? repeat;
        private readonly PcmLoopPlayback? playback;
        private readonly int rate;
        private readonly int channels;
        private readonly int nativeRate;
        private int primaryFrequency;
        private readonly int secondaryFrequency;
        private long nativePanVersion;
        private long nativeFrequencyVersion;
        private readonly int start;
        private readonly int end;
        private readonly string fingerprint;
        public int Sample { get; }
        public int NativeVolume { get; private set; }
        public bool Loop { get; }
        public double HandoffRemainingSeconds => playback is { LoopStarted: false } ? playback.CaptureHandoffRemainingSeconds(0, 44100) : double.PositiveInfinity;
        public bool IsPlaying => primary.IsPlaying || repeat?.IsPlaying == true;

        public Voice(PcmAudioOutput output, byte[] pcm, int rate, int channels, int pan, int gain, int sample, bool loop, int start, int end, string fingerprint, Ff7SoundVoicePosition? resume, int nativeVolume, int nativeRate, int nativePitch, double? nativeLoopDelaySeconds, Func<double>? elapsedSeconds, long nativePanVersion, long nativeFrequencyVersion)
        {
            this.nativeFrequencyVersion = resume?.NativeFrequencyVersion ?? nativeFrequencyVersion;
            this.nativePanVersion = resume?.NativePanVersion ?? nativePanVersion;
            this.rate = rate;
            this.channels = channels;
            this.nativeRate = nativeRate;
            int requestedFrequency = Ff7NativeSoundFrequency.Frequency(nativeRate, nativePitch);
            int initialFrequency = Ff7NativeSoundFrequency.Accepted(requestedFrequency) ? requestedFrequency : nativeRate;
            primaryFrequency = resume?.PrimaryFrequency is > 0 ? resume.PrimaryFrequency : resume is not null ? nativeRate : initialFrequency;
            secondaryFrequency = resume?.SecondaryFrequency is > 0 ? resume.SecondaryFrequency : resume is not null ? nativeRate : initialFrequency;
            this.start = loop ? start : 0;
            this.end = loop ? end : pcm.Length;
            this.fingerprint = fingerprint;
            Sample = sample;
            NativeVolume = nativeVolume;
            Loop = loop;
            int alignment = channels * sizeof(short);
            int position = checked((int)(resume?.PositionFrames ?? 0) * alignment);
            primary = output.Create(loop ? pcm.AsSpan(0, end).ToArray() : pcm, rate, channels);
            primary.FrequencyRatio(primaryFrequency / (double)nativeRate);
            primary.Pan(pan);
            primary.Volume(gain);
            if (loop && start != 0)
            {
                repeat = output.Create(pcm.AsSpan(start, end - start).ToArray(), rate, channels);
                repeat.FrequencyRatio(secondaryFrequency / (double)nativeRate);
                repeat.Pan(resume?.SecondaryPan ?? 0);
                repeat.Volume(gain);
                int bytesPerSecond = checked(rate * alignment);
                bool repeating = resume?.LoopStarted == true;
                playback = new PcmLoopPlayback(primary, repeat, 127, resume?.LoopHandoffRemainingSeconds ?? nativeLoopDelaySeconds ?? (start - position) / (double)bytesPerSecond, bytesPerSecond, end - start, restoredLoop: repeating, blockAlignment: alignment, elapsedSeconds: elapsedSeconds);
                if (repeating)
                {
                    repeat.Position(position - start);
                    repeat.Play(true);
                }
                else
                {
                    if (position == end)
                    {
                        primary.PositionAtEnd();
                    }
                    else
                    {
                        primary.Position(position);
                        primary.Play(false);
                    }
                }
            }
            else
            {
                primary.Position(position);
                primary.Play(loop);
            }
        }

        public Ff7SoundVoicePosition? Capture(int owner, int queued)
        {
            if (Sample < 1 || fingerprint.Length != 64)
            {
                return null;
            }
            int alignment = channels * sizeof(short);
            (int bytes, bool repeating) = playback?.CapturePosition(queued, start) ?? (primary.PositionBytesBeforeQueuedFrames(queued), false);
            if (bytes >= end)
            {
                if (!Loop)
                {
                    return null;
                }
                if (repeating || playback is null)
                {
                    bytes = start + (bytes - start) % (end - start);
                }
            }
            double? handoff = playback is not null && !repeating ? playback.CaptureHandoffRemainingSeconds(queued, 44100) : null;
            return new(owner, Sample, fingerprint, rate, channels, bytes / alignment, Loop, start / alignment, end / alignment, repeating, primary.PanValue, primary.GainValue, NativeVolume, nativeRate, primaryFrequency, secondaryFrequency, handoff, nativePanVersion, repeat?.PanValue, nativeFrequencyVersion);
        }

        public void PublishFrequency(int frequency, long version)
        {
            if (nativeFrequencyVersion == version)
            {
                return;
            }
            if (Ff7NativeSoundFrequency.Accepted(frequency))
            {
                SetPrimaryFrequency(frequency);
            }
            nativeFrequencyVersion = version;
        }

        public void PublishPan(int pan, long version)
        {
            if (nativePanVersion == version)
            {
                return;
            }
            primary.Pan(pan);
            nativePanVersion = version;
        }

        public void Update(double? elapsed)
        {
            if (elapsed is double seconds)
            {
                playback?.UpdateAt(seconds);
            }
            else
            {
                playback?.Update();
            }
        }

        public void Pause(bool paused)
        {
            if (playback is not null)
            {
                playback.Pause(paused);
            }
            else if (paused)
            {
                primary.Pause();
            }
            else
            {
                primary.Resume();
            }
        }

        public void SetTempo(double speed)
        {
            if (playback is not null)
            {
                playback.SetTempo(speed);
            }
            else
            {
                primary.Tempo(speed);
            }
        }

        public void SetPrimaryFrequency(int frequency)
        {
            primaryFrequency = frequency;
            primary.FrequencyRatio(frequency / (double)nativeRate);
        }

        public void SetGain(int gain, int nativeVolume)
        {
            NativeVolume = nativeVolume;
            primary.Volume(gain);
            repeat?.Volume(gain);
        }

        public void Dispose()
        {
            if (playback is not null)
            {
                playback.Dispose();
            }
            else
            {
                primary.Stop();
                primary.Dispose();
            }
        }
    }
}
