namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleActionMemory nativeActionMemory = NativeBattleActionMemory.Unknown();
    public NativeBattleActionMemory NativeActionMemory => nativeActionMemory;

    private void RestoreNativeActionMemory(NativeBattleActionMemory? saved)
    {
        NativeBattleActionMemory memory = saved ?? throw new InvalidDataException("Native B98..B9F memory is missing; explicitly migrate the standalone snapshot.");
        memory.Validate();
        nativeActionMemory = memory;
    }

    private void RestoreNativeActorActionFacts(BattleCombatant actor, NativeBattleParticipantSnapshot saved)
    {
        actor.LastNativeAction = saved.LastAction;
        actor.LastNativeHealing = saved.LastHealing;
    }
}
