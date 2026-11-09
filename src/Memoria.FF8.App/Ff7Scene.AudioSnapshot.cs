using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7SoundVoicePosition[]? pendingSounds;
    private Ff7MusicVoicePosition? pendingMusic;
    private Ff7MusicVoicePosition? musicSource;
    private bool legacyMusicPosition;

    private (PcmAudio Pcm, bool Streamed, bool Loop, int LoopStart, int LoopEnd, string Fingerprint, int NativeRate, double? LoopDelay) DecodeSound(int soundId)
    {
        if (!soundCache.TryGetValue(soundId - 1, out var decoded))
        {
            Ff7SoundSample sample = Ff7SoundResources.Read(installation, soundId - 1);
            PcmAudio pcm = PcmAudioDecoder.ReadWave(sample.WaveBytes);
            (int start, int end) = sample.Loop ? Ff7SoundChannels.ConvertLoopBounds(sample, pcm.SampleRate, pcm.Channels) : default;
            decoded = (pcm, !sample.Loop && sample.CompressedByteLength > 0x20000, sample.Loop, start, end, Convert.ToHexString(SHA256.HashData(sample.WaveBytes)), sample.NativeSampleRate, sample.Loop && sample.LoopStartPcmByte != 0 ? Ff7NativeSoundFrequency.LoopDelayMilliseconds(sample.LoopStartPcmByte, sample.NativeSampleRate) / 1000d : null);
            soundCache.Add(soundId - 1, decoded);
        }
        return decoded;
    }

    private Ff7AudioPresentationState CaptureAudioPresentation()
    {
        ApplyNativeResidentStops();
        RefreshNativePans();
        RefreshNativeFrequencies();
        Ff7AudioPresentationState Capture(int queued)
        {
            Ff7MusicVoicePosition? music = pendingMusic;
            if (musicPlayback is not null && musicSource is Ff7MusicVoicePosition source)
            {
                int alignment = source.Channels * sizeof(short);
                (int bytes, bool repeating) = musicPlayback.CapturePosition(queued, source.LoopStartFrames * alignment);
                long frames = bytes / alignment;
                if (frames >= source.LoopEndFrames)
                {
                    frames = source.LoopStartFrames + (frames - source.LoopStartFrames) % (source.LoopEndFrames - source.LoopStartFrames);
                    repeating = true;
                }
                music = source with { PositionFrames = frames, LoopStarted = repeating };
            }
            return new() { Sounds = pendingSounds ?? soundChannels.Capture(queued), Music = music };
        }
        return audioOutput is null ? Capture(0) : audioOutput.CapturePlaybackPosition(Capture);
    }

    private void RestoreAudioPresentation(Ff7AudioPresentationState? saved, bool restoring)
    {
        pendingSounds = null;
        pendingMusic = null;
        legacyMusicPosition = restoring && saved is null;
        if (saved is null)
        {
            if (restoring)
            {
                Console.Error.WriteLine("FFVII compatibility: this save has no captured audio presentation; SFX reset and music resumes approximately from recorded music time.");
            }
            return;
        }
        if (saved.Version != 1)
        {
            Console.Error.WriteLine("FFVII compatibility: unsupported optional audio presentation was reset; recorded progress is preserved.");
            return;
        }
        saved.Validate();
        pendingSounds = saved.Sounds;
        pendingMusic = saved.Music;
    }

    private void RestorePendingSounds()
    {
        if (pendingSounds is not Ff7SoundVoicePosition[] saved || !audioEnabled)
        {
            return;
        }
        pendingSounds = null;
        foreach (Ff7SoundVoicePosition position in saved.OrderByDescending(voice => voice.Channel))
        {
            try
            {
                var decoded = DecodeSound(position.SoundId);
                audioOutput ??= new PcmAudioOutput();
                if (!soundChannels.RestoreVoice(audioOutput, decoded.Pcm.Bytes, decoded.Pcm.SampleRate, decoded.Pcm.Channels, position.SoundId, decoded.Streamed, decoded.Loop, decoded.LoopStart, decoded.LoopEnd, decoded.Fingerprint, position, decoded.NativeRate, decoded.LoopDelay))
                {
                    Console.Error.WriteLine("FFVII compatibility: a saved SFX resource/format no longer matches the selected installation; that presentation voice was reset.");
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentOutOfRangeException)
            {
                Console.Error.WriteLine("FFVII compatibility: a saved SFX resource is unavailable; that presentation voice was reset.");
            }
        }
        soundChannels.Pause(audioPaused);
    }

    private Ff7MusicVoicePosition? ResumeMusicPosition(int track, string fingerprint, PcmAudio pcm, ExternalMusicLoop loop)
    {
        Ff7MusicVoicePosition? saved = pendingMusic;
        pendingMusic = null;
        if (saved is null)
        {
            return null;
        }
        int alignment = pcm.Channels * sizeof(short);
        if (saved.Track == track && saved.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase) && saved.SampleRate == pcm.SampleRate && saved.Channels == pcm.Channels && saved.LoopStartFrames == loop.StartByte / alignment && saved.LoopEndFrames == loop.EndByte / alignment)
        {
            return saved;
        }
        Console.Error.WriteLine("FFVII compatibility: saved music resource/format no longer matches the selected track; its presentation restarts.");
        return null;
    }
}
