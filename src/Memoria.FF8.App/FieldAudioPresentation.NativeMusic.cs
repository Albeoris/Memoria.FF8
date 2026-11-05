using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    internal static void DispatchNativeMusicResume(NativeMusicPlaybackState? playback, Action<int, int, int> play, Action<uint, int>? concert = null)
    {
        if (playback is null)
        {
            throw new NotSupportedException("Music resume requires the retained native playback owner.");
        }
        NativeMusicPlaybackMode first = playback.ReadMode(0);
        NativeMusicPlaybackMode second = playback.ReadMode(1);
        if (first != NativeMusicPlaybackMode.Stopped && first == second)
        {
            throw new NotSupportedException("Two selected slots share one native backend; its current device selection is not retained.");
        }
        List<(int Slot, int Id, int Gain)> selected = [];
        Prepare(0, first, playback.CurrentMusicId0, playback.Gain0);
        Prepare(1, second, playback.CurrentMusicId1, playback.Gain1);
        foreach (var selection in selected)
        {
            if (selection.Slot == 0 && playback.ConcertMask is uint mask)
            {
                concert!(mask, selection.Gain);
                continue;
            }
            play(selection.Slot, selection.Id, selection.Gain);
        }

        void Prepare(int slot, NativeMusicPlaybackMode mode, int? id, int? gain)
        {
            if (mode == NativeMusicPlaybackMode.Stopped)
            {
                return;
            }
            int music = id ?? throw new NotSupportedException("Music resume requires the selected slot's native current ID.");
            int volume = gain ?? throw new NotSupportedException("Music resume requires the selected slot's native gain latch.");
            if (music == 43)
            {
                if (slot != 0 || playback.ConcertMask is null || concert is null)
                {
                    throw new NotSupportedException("Music43 resume requires its retained concert selection and output route.");
                }
            }
            if (mode != NativeMusicRouting.Mode46B530(music))
            {
                throw new InvalidDataException("Music resume mode disagrees with the native current ID's resource route.");
            }
            if (volume is < 0 or > 127 || music is < 0 or >= 117)
            {
                throw new NotSupportedException("Music resume requires an available native resource and reached device gain-table index.");
            }
            selected.Add((slot, music, volume));
        }
    }

    private void ExecuteMusicPlayback(NativeMusicPlaybackDecision? decision, int fieldId, int startPoint = 0, int finalDuration = 0, int? initialGain = null, uint? choiceMask = null, bool noIntro = false)
    {
        DispatchNativeMusicPlayback(decision, StopMusicSlot, (slot, music, gain) => PlayMusic(music, slot, gain, fieldId, startPoint, forceRestart: true, identityResolved: true, noIntro: noIntro), (slot, gain) => SetMusicGain(slot, null, gain, finalDuration), () =>
        {
            uint mask = choiceMask ?? throw new InvalidDataException("Native concert output requires its executed instrument-selection mask.");
            PlayConcert(mask, (int)midiVolume);
        }, initialGain);
    }

    private void PlayConcert(uint mask, int gain)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gain);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gain, 127);
        midiVolume = gain;
        midiRamp = null;
        IReadOnlyList<string> tracks = Memoria.FF8.Assets.NativeConcertSelection.Tracks(mask);
        if (tracks.Count == 0)
        {
            CancelMusicDecode(MusicKind.Midi);
            StopExternalMusic(MusicKind.Midi);
            music?.Stop();
            musicKinds[0] = MusicKind.Midi;
            musicIds[0] = 43;
            nativeMidiPaused = false;
            return;
        }
        if (TryPlayExternalConcert(mask))
        {
            return;
        }
        string[] paths = tracks.Select(track => gameFiles.ResolveDataFile($"music/dmusic/{track}")).ToArray();
        CancelMusicDecode(MusicKind.Midi);
        StopExternalMusic(MusicKind.Midi);
        int outputGain = OutputGain(Catalog.MusicVolume(gain), musicPreference);
        if (ResumeSegmentPosition(0, 43) is { } position)
        {
            Music.PlayConcertAtPosition(paths, outputGain, position);
        }
        else
        {
            Music.PlayConcert(paths, outputGain);
        }
        RegisterSegmentPosition(0);
        DiscardMusicResumePosition(0);
        if (focusPaused)
        {
            Music.Pause();
        }
        musicKinds[0] = MusicKind.Midi;
        musicIds[0] = 43;
        nativeMidiPaused = false;
    }

    internal static void DispatchNativeMusicPlayback(NativeMusicPlaybackDecision? decision, Action<int> stop, Action<int, int, int> play, Action<int, int> gain, Action concert, int? initialGain = null)
    {
        if (decision is null)
        {
            throw new NotSupportedException("Native music output requires its executed playback decision; historical request fields cannot reconstruct it.");
        }
        decision.Validate();
        if (initialGain is int initial)
        {
            gain(decision.Channel, initial);
        }
        if (decision.StopRequested)
        {
            stop(decision.Channel);
        }
        if (decision.ConcertRequested)
        {
            concert();
        }
        if (decision.PlayRequested)
        {
            play(decision.Channel, decision.MusicId!.Value, decision.PlayGain!.Value);
        }
        if (decision.FinalGain is int final)
        {
            gain(decision.Channel, final);
        }
    }

    internal static void DispatchNativeMusicStop(NativeMusicStop request, Action<int> stop)
    {
        if (request.StopRequested == true)
        {
            DispatchNativeMusicChannel(request.PriorChannelId ?? throw new NotSupportedException("Native music stop requires its actual retained channel DWORD."), stop);
        }
    }

    internal static void DispatchNativeMusicVolume(NativeMusicVolume request, Action<int, int> gain)
    {
        int channel = request.ChannelId ?? throw new NotSupportedException("Native music volume requires its actual retained channel DWORD.");
        if (request.BothChannels != (unchecked((uint)channel) > 1))
        {
            throw new InvalidDataException("Native music volume routing disagrees with its retained channel DWORD.");
        }
        DispatchNativeMusicChannel(channel, slot => gain(slot, request.RequestedVolume));
    }

    internal static void DispatchNativeMusicInitialization(NativeSoundInitialization request, Action<int, int> gain)
    {
        var channels = request.MusicGainChannels ?? throw new NotSupportedException("Native SOINIT output requires its executed ordered channel calls.");
        if (channels.IsDefault)
        {
            throw new InvalidDataException("Native SOINIT output requires its populated ordered channel array.");
        }
        foreach (int channel in channels)
        {
            if (channel == -1)
            {
                throw new InvalidDataException("Native SOINIT skips channel -1 instead of broadcasting its gain.");
            }
            DispatchNativeMusicChannel(channel, slot => gain(slot, 127));
        }
    }

    private static void DispatchNativeMusicChannel(int channel, Action<int> operation)
    {
        if (unchecked((uint)channel) > 1)
        {
            operation(0);
            operation(1);
            return;
        }
        operation(channel);
    }
}
