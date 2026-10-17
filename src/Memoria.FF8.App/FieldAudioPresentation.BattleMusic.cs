using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
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
