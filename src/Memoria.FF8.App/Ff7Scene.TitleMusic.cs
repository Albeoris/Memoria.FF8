using Memoria.FF7.Resources;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private void UpdateTitleAudio()
    {
        if (!audioEnabled)
        {
            return;
        }
        if (musicDecode is null && musicPlayback is null)
        {
            audioNative ??= Ff7PcExecutableData.Load(installation);
            BeginMusicDecode(audioNative.MusicNames[ResolveTitleMusicTrack(audioNative.MusicNames)]);
        }
        int volume = dialoguePreferences?.Invoke().MusicVolume ?? 100;
        if (musicDecode is { IsCompleted: true })
        {
            (PcmAudio pcm, ExternalMusicLoop loop, _) = musicDecode.GetAwaiter().GetResult();
            musicDecode = null;
            audioOutput ??= new PcmAudioOutput();
            musicPlayback = CreateTitleMusicPlayback(audioOutput, pcm, loop, volume, audioPaused);
            Console.WriteLine($"ff7-title-music=Playing track=kurai loopStart={loop.StartByte} loopEnd={loop.EndByte}");
        }
        musicPlayback?.SetGain(FieldAudioPresentation.OutputGain(0, volume));
        musicPlayback?.Update();
        audioOutput?.ThrowIfFailed();
    }

    internal static int ResolveTitleMusicTrack(IReadOnlyList<string> names)
    {
        for (int track = 1; track < names.Count; track++)
        {
            if (string.Equals(names[track], "kurai", StringComparison.OrdinalIgnoreCase))
            {
                return track;
            }
        }
        throw new NotSupportedException("The selected FFVII music catalog has no Anxious Heart track (kurai).");
    }

    internal static PcmLoopPlayback CreateTitleMusicPlayback(PcmAudioOutput output, PcmAudio pcm, ExternalMusicLoop loop, int volume, bool paused)
    {
        PcmAudioOutput.Buffer primary = output.Create(pcm.Bytes.AsSpan(0, loop.EndByte).ToArray(), pcm.SampleRate, pcm.Channels);
        PcmAudioOutput.Buffer repeat = output.Create(pcm.Bytes.AsSpan(loop.StartByte, loop.EndByte - loop.StartByte).ToArray(), pcm.SampleRate, pcm.Channels);
        int bytesPerSecond = checked(pcm.SampleRate * pcm.Channels * sizeof(short));
        PcmLoopPlayback playback = new(primary, repeat, 100, loop.EndByte / (double)bytesPerSecond, bytesPerSecond, loop.EndByte - loop.StartByte, blockAlignment: pcm.Channels * sizeof(short));
        playback.SetGain(FieldAudioPresentation.OutputGain(0, volume));
        primary.Play(false);
        playback.Pause(paused);
        return playback;
    }
}
