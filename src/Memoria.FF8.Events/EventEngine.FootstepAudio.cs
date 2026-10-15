using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Transient output of the original animation contact callback, outside saved VM state.</summary>
public sealed record NativeFootstepSound(int ObjectId, int Mode, uint EffectId, uint Pan, uint Volume, uint Flags);

public sealed partial class EventEngine
{
    public Func<ActorState, int, NativeFootstepSound>? ResolveFootstepSound { get; set; }

    private void StepNativeFootstepCrossings(ActorState actor, ushort oldFrame, ushort advancedFrame, ushort total, ushort increment, bool narrow)
    {
        if (!actor.FootstepsEnabled || ResolveFootstepSound is null)
        {
            return;
        }
        // 478270 narrows each difference to signed WORD; 529FF0 promotes its
        // signed phase words before subtraction. Both inspect the raw advance
        // before the renderer/header wraps, in mode1 then mode0 order.
        int first = narrow ? oldFrame : unchecked((short)oldFrame);
        int next = narrow ? advancedFrame : unchecked((short)advancedFrame);
        int half = (narrow ? total - unchecked((short)increment) : unchecked((short)total) - unchecked((short)increment)) >> 1;
        Contact(128, 1);
        Contact(half + 128, 0);

        void Contact(int threshold, int mode)
        {
            int before = narrow ? unchecked((short)(first - threshold)) : first - threshold;
            int after = narrow ? unchecked((short)(next - threshold)) : next - threshold;
            if ((before < 0) != (after < 0))
            {
                NativeFootstepSound sound = ResolveFootstepSound(actor, mode);
                NativeGlobalSoundProcess process = services.GlobalSoundProcess ?? throw new NotSupportedException("Native footstep contact requires the persistent global sound process.");
                //46B2A0 stores the original effect ID even with no native audio buffers.
                //The explicit footstep masks preserve rotation and replace IDs23/24.
                services.SetGlobalSoundProcess(process.RecordEffect(sound.EffectId, sound.Flags, out _));
                AudioCommandExecuted?.Invoke(sound);
            }
        }
    }

    private bool HasNativeMode1(ActorState actor)
    {
        return actor.NativeMovementCompletion542 != 2 && actor.NativeLadder is null && actor.NativeLadderMode3 is null && actor.NativeJump is null && (actor.NativeScriptMovement is { Active: true, Suspended: false } || partyJoin?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true || partySplit?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true);
    }
}
