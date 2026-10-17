using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private void PauseMusicOutput()
    {
        if (streamedMusic is not null && !streamedMusicPaused)
        {
            streamedMusic.Stop();
            streamedMusicPaused = true;
        }
        music?.Pause();
    }

    private void ResumeMusicOutput()
    {
        if (streamedMusic is not null && streamedMusicPaused)
        {
            streamedMusic.Play(true);
            streamedMusicPaused = false;
        }
        music?.Resume();
    }

    private void ExecuteBattleMusicCommand(NativeBattleMusicCommandReceipt command)
    {
        DispatchNativeBattleMusicCommand(command, PauseMusicOutput, ResumeMusicOutput, StopMusicSlot, decision => ExecuteMusicPlayback(decision, -1), (channel, initial, gain, duration) => DispatchNativeMusicChannel(channel, slot => SetMusicGain(slot, initial, gain, duration)));
    }

    internal static void DispatchNativeBattleMusicCommand(NativeBattleMusicCommandReceipt command, Action pause, Action resume, Action<int> stop, Action<NativeMusicPlaybackDecision> playback, Action<ushort, int?, int, int> gain)
    {
        command.Playback?.Validate();
        bool valid = command.Command switch
        {
            1 => command is { Playback: null, GainChannel: null, Gain: null, InitialGain: null, EffectiveDuration: 0, PauseOutput: false, ResumeOutput: false },
            2 => command is { Playback: { Channel: 0, ConcertRequested: false, FinalGain: null }, GainChannel: null, Gain: null, InitialGain: null, EffectiveDuration: 0, PauseOutput: true, ResumeOutput: false },
            3 => command is { Playback: null, GainChannel: not null, Gain: >= 0 and <= 127, InitialGain: null or 0, EffectiveDuration: >= 0 and <= 255, PauseOutput: false, ResumeOutput: true },
            4 => command is { Playback: null, GainChannel: 0, Gain: 0, InitialGain: null, EffectiveDuration: >= 0 and <= 255, PauseOutput: false, ResumeOutput: false },
            _ => command is { Playback: null, GainChannel: null, Gain: null, InitialGain: null, EffectiveDuration: 0, PauseOutput: false, ResumeOutput: false }
        };
        if (!valid)
        {
            throw new NotSupportedException("Native501B60 output requires its executed command branch and available device gain operands.");
        }
        if (command.Command == 1)
        {
            stop(0);
            stop(1);
        }
        if (command.PauseOutput)
        {
            pause();
        }
        if (command.Playback is { } decision)
        {
            playback(decision);
        }
        if (command.GainChannel is ushort channel)
        {
            gain(channel, command.InitialGain, command.Gain!.Value, command.EffectiveDuration);
        }
        if (command.ResumeOutput)
        {
            resume();
        }
    }

    private void ExecuteBattleMusicReturn(NativeBattleMusicReturnReceipt returned)
    {
        DispatchNativeBattleMusicReturn(returned, StopSounds, decision => ExecuteMusicPlayback(decision, -1), (channel, duration, target) => DispatchNativeMusicChannel(channel, slot => SetMusicGain(slot, null, target, duration)));
    }

    internal static void DispatchNativeBattleMusicReturn(NativeBattleMusicReturnReceipt returned, Action stopSounds, Action<NativeMusicPlaybackDecision> playback, Action<ushort, byte, int> fade)
    {
        returned.Validate();
        stopSounds();
        if (returned.Playback is { } decision)
        {
            playback(decision);
        }
        if (returned.FadeChannel is ushort channel)
        {
            fade(channel, returned.EffectiveDuration, returned.FadeTarget!.Value);
        }
    }

    private void ExecuteBattleMusicEntry(NativeBattleMusicEntryReceipt entry)
    {
        DispatchNativeBattleMusicEntry(entry, StopMusicSlot, decision => ExecuteMusicPlayback(decision, -1), StopSounds, () => SetAllVoiceVolumes(0, 32, 127, 0), PlayBattleModuleSound);
    }

    internal static void DispatchNativeBattleMusicEntry(NativeBattleMusicEntryReceipt entry, Action<int> stopMusic, Action<NativeMusicPlaybackDecision> playback, Action stopSounds, Action resetSoundGain, Action<NativeBattleModuleSound> playSound)
    {
        entry.Validate();
        if (entry.EarlyStop)
        {
            stopMusic(0);
        }
        if (entry.Playback is { } decision)
        {
            playback(decision);
        }
        stopSounds();
        resetSoundGain();
        foreach (NativeBattleModuleSound sound in entry.Sounds)
        {
            playSound(sound);
        }
    }
}
