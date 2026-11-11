using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private Ff7BattleActionState StartPrimaryWave(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleWave(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask) };
        return action with { WaveStarted = true, PrimaryCallbacksRunning = true };
    }

    private void ApplyPrimaryEffectReceipts(Ff7BattlePrimaryState primary)
    {
        ApplyTailLaserSecondaryReceipts(primary);
        if (primary.WaveSparks.Count > 0)
        {
            Ff7BattleSecondaryState secondary = EnsureSecondaryEffects();
            foreach (Ff7BattleBraverPoint position in primary.WaveSparks)
            {
                secondary = Ff7BattleSecondaryEffects.ScheduleWaveSpark(secondary, position);
            }
            state = state with { SecondaryEffects = secondary };
        }
        foreach (Ff7BattlePrimaryPositionedSound cue in primary.PositionedSounds)
        {
            byte pan = cue.Actor == -1 ? cue.FallbackPan : Ff7NativeBattleSound.ActorPan(SoundScreenX(cue.Actor));
            Ff7BattleSoundState sounds = state.Sounds ?? Ff7BattleSoundState.Empty;
            AppendSounds(cue.RawPan ? Ff7NativeBattleSound.EmitRawPan(sounds, cue.Sound, pan, cue.Channel) : Ff7NativeBattleSound.Emit(sounds, cue.Sound, pan, cue.Channel));
        }
        ApplyPrimaryCameraFocusY(primary.CameraFocusYDelta);
    }
}
