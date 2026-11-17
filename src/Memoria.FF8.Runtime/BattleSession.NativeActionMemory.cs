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
            actor.LastNativeAction = saved.LastAction;
            actor.LastNativeHealing = saved.LastHealing;
            return;
    }
}
