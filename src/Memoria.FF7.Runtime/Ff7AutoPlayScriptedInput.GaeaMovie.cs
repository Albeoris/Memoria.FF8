using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    // Reviewed native no-input movie return (NG-114).
    internal static bool IsNativeGaeaMovieNeutralEntry(Ff7AutoPlayState state, long generation, FieldVmSnapshot vm, Ff7FieldProcess process)
    {
        return HasNativeGaeaMovieInputContext(state, vm, process) && (vm.PersistentVariables[0x83] & 1) == 0 && process.Lines.Any(line => line.Entity == 5 && line.Enabled == 1 && line.Touch) && state.Active is { Entity: 5, Script: 4, Kind: Ff7AutoPlayTargetKind.LineTouch, Accepted: false, Returned: false, ExecutionProvenance: null } active && active.Generation == generation;
    }

    internal static bool IsNativeGaeaMovieNeutralReturn(Ff7AutoPlayState state, long generation, FieldVmSnapshot vm, Ff7FieldProcess process)
    {
        if (!HasNativeGaeaMovieInputContext(state, vm, process) || (vm.PersistentVariables[0x83] & 1) == 0 || process.Movie is not { PreparedId: 20, Completed: true, Playing: false } || state.Active is not { Entity: 5, Script: 4, Kind: Ff7AutoPlayTargetKind.LineTouch, Accepted: true, Returned: false, ExecutionProvenance.RootReturned: false } active || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || vm.Entities.Length <= 5 || vm.Entities[5] is not { Priority: 1, ProgramCounter: 0x33D })
        {
            return false;
        }
        IReadOnlyDictionary<int, int> owners = Ff7AutoPlayExecutionOwners.ReadCurrentOwners(vm, active, generation);
        return owners.TryGetValue(5, out int priority) && priority == 1 && !(vm.PendingRequests ?? []).Any(request => owners.TryGetValue(request.Entity, out int ownedPriority) && request.Priority == ownedPriority);
    }

    private static bool HasNativeGaeaMovieInputContext(Ff7AutoPlayState state, FieldVmSnapshot vm, Ff7FieldProcess process)
    {
        return state.DestinationField == 700 && state.Choice is null && state.PendingRestore is null && vm.Initialized && vm.ProgramIdentity == Ff7AutoPlayPolicy.GaeaMovieProgram && vm.PersistentVariables.Length > 0x83 && !process.PlayerLocked && !process.MenuDisabled && process.GlobalCommand == Ff7GlobalCommand.None && process.Transition is null && process.Join is null && process.Movie is not { Playing: true } && process.Menu is not { Completed: false } && !process.Dialogues.Any(dialogue => !dialogue.Completed) && process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is { EntityBound: true, Entity: 8, Ladder: null, Jump: null } player && !process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed);
    }
}
