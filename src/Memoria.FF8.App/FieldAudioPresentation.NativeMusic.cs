using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private void ExecuteMusicPlayback(NativeMusicPlaybackDecision? decision, int fieldId, int startPoint = 0, int finalDuration = 0, int? initialGain = null, uint? choiceMask = null)
    {
        DispatchNativeMusicPlayback(decision, StopMusicSlot, (slot, music, gain) => PlayMusic(music, slot, gain, fieldId, startPoint, forceRestart: true, identityResolved: true), (slot, gain) => SetMusicGain(slot, null, gain, finalDuration), () => RequireOrdinaryMusicOutput(choiceMask ?? throw new InvalidDataException("Native concert output requires its executed instrument-selection mask.")), initialGain);
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
