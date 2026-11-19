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
        if (musicPlayback is null && musicDecoder?.IsPending(MusicDecodeOwner) != true)
        {
            audioNative ??= Ff7PcExecutableData.Load(installation);
            BeginMusicDecode(audioNative.MusicNames[ResolveTitleMusicTrack(audioNative.MusicNames)], PcmMusicRepeat.SeparateLoop, (prepared, _) => StartTitleMusic(prepared));
        }
        musicDecoder?.Pump();
        int volume = dialoguePreferences?.Invoke().MusicVolume ?? 100;
        musicPlayback?.SetGain(FieldAudioPresentation.OutputGain(0, volume));
        musicPlayback?.Update();
        audioOutput.ThrowIfFailed();
    }

    private void StartTitleMusic(PreparedPcmMusic prepared)
    {
        int volume = dialoguePreferences?.Invoke().MusicVolume ?? 100;
        musicPlayback = CreateTitleMusicPlayback(audioOutput, prepared, volume, audioPaused);
        Console.WriteLine($"ff7-title-music=Playing track=kurai loopStart={prepared.Loop.StartByte} loopEnd={prepared.Loop.EndByte}");
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

    internal static PcmLoopPlayback CreateTitleMusicPlayback(PcmAudioOutput output, PreparedPcmMusic music, int volume, bool paused)
    {
        return music.Start(output, 0, false, FieldAudioPresentation.OutputGain(0, volume), 100, paused);
    }
}
