namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly NativeBattleActionMemoryOwner nativeActionMemory = new();
    public NativeBattleActionMemory NativeActionMemory => nativeActionMemory.State;

    private void RestoreNativeActionMemory(NativeBattleActionMemory? saved)
    {
        NativeBattleActionMemory memory = saved ?? throw new InvalidDataException("Native B98..B9F memory is missing; explicitly migrate the standalone snapshot.");
        memory.Validate();
        nativeActionMemory.State = memory;
    }

    private void ValidateNativeActionMemoryProjection(BattleCombatant actor, NativeBattleParticipantSnapshot saved)
    {
        if (saved.LastAction != actor.LastNativeAction || saved.LastHealing != actor.LastNativeHealing)
        {
            throw new InvalidDataException("Native participant action/healing projection disagrees with its sole physical B98..B9F owner.");
        }
    }
}
