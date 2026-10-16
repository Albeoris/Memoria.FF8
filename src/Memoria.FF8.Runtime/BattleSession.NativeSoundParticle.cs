namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState ScheduleNativeSoundParticle(NativeBattleGraphicsState graphics, int model, NativeBattleAuxiliaryProgram program, bool soundTwo, byte bone)
    {
        if (!graphics.Auxiliary.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return graphics;
        }
        NativeGlobalRandomState before = nativeGlobalRandom ?? throw new NotSupportedException("Native50F7D0 requires the actual534AA0 random owner.");
        (NativeBattleAuxiliaryState auxiliary, NativeGlobalRandomState after, bool allocated) = graphics.Auxiliary.AppendSoundParticle(model, soundTwo, bone, program, before);
        nativeGlobalRandom = after;
        if (allocated)
        {
            nativeGlobalRandomCalls = checked(nativeGlobalRandomCalls + 1);
        }
        return graphics with { Auxiliary = auxiliary };
    }
}
