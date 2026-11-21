using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    // Reviewed native altar input strategy (NG-102).
    private const string AltarInputProgram = "C69128E2485D4AC0863A6C58078468395BD9D9A70B6EC2EB479FC97A2B2FCEE2";

    private Ff7AutoplayInput? ChooseAltarInput(Ff7AutoPlayActiveEvent? active)
    {
        if (active is { Accepted: true })
        {
            return null;
        }
        Ff7FieldProcess process = session.Host.Process;
        if (process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return null;
        }
        FieldVmSnapshot vm = session.Machine.Capture();
        FieldScriptInputDemand[] demands = session.Machine.ReadCurrentInputDemands().Where(demand => IsAltarInput(vm, process, demand)).ToArray();
        if (demands.Length != 1)
        {
            return null;
        }
        CurrentDemand = demands[0];
        return SelectInput(CurrentDemand, session.Machine.Input);
    }

    internal static bool IsAltarInput(FieldVmSnapshot vm, Ff7FieldProcess process, FieldScriptInputDemand demand)
    {
        if (vm.ProgramIdentity != AltarInputProgram || demand.ProgramIdentity != vm.ProgramIdentity || demand is not { Entity: 0, Priority: 7, Kind: FieldScriptInputKind.Pressed, Mask: 0x20, Mapped: false, AwaitingInput: true } || vm.Entities.Length <= 4 || vm.Entities[0].Priority != 7 || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || process.Dialogues.Any(dialogue => !dialogue.Completed) || (vm.PendingRequests ?? []).Length != 0)
        {
            return false;
        }
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        int story = variables.ReadWord(FieldBank.Persistent0Word, 0);
        int source = vm.Entities[0].ProgramCounter;
        bool first = story == 669 && demand is { ProgramCounter: 0x02D7, PresentationPolling: true } && source is 0x02C0 or 0x02C9 or 0x02D7 or 0x03D4;
        bool second = story == 671 && demand.ProgramCounter == 0x02F0 && source == 0x02F0;
        int player = first ? 3 : 4;
        return (first || second) && vm.Entities[3].Priority == 7 && vm.Entities[4].Priority == 7 && vm.Entities.Take(5).All(entity => entity.SyncStates.All(state => state == FieldSyncState.None) && entity.SyncOwners.All(owner => owner == -1)) && process.Actors.Count(actor => actor.PlayerControlled) == 1 && process.Actors.Any(actor => actor.EntityBound && actor.PlayerControlled && actor.Entity == player);
    }

    internal static FieldScriptInputDemand? SelectAltarForwardInput(FieldVmSnapshot vm, Ff7FieldProcess process, IReadOnlyList<FieldScriptInputDemand> demands)
    {
        if (vm.ProgramIdentity != AltarInputProgram || vm.Entities.Length <= 4 || vm.Entities[0].Priority != 7 || vm.Entities[4].Priority != 7 || !vm.Entities.Take(5).All(entity => entity.SyncStates.All(state => state == FieldSyncState.None) && entity.SyncOwners.All(owner => owner == -1)) || (vm.PendingRequests ?? []).Length != 0 || !process.PlayerLocked || !process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || process.Dialogues.Any(dialogue => !dialogue.Completed) || new FieldVariables(vm.PersistentVariables, vm.MapVariables).ReadWord(FieldBank.Persistent0Word, 0) != 671 || !process.Actors.Any(actor => actor.PlayerControlled && actor.EntityBound && actor.Entity == 4))
        {
            return null;
        }
        int source = vm.Entities[0].ProgramCounter;
        bool CurrentSource(FieldScriptInputDemand demand) => demand.ProgramCounter switch
        {
            0x031E => source is 0x02FB or 0x0302 or 0x0309 or 0x0310 or 0x0317 or 0x031E or 0x03CF,
            0x0348 => source is 0x0325 or 0x032C or 0x0333 or 0x033A or 0x0341 or 0x0348 or 0x03CD,
            0x034F => source is 0x034F or 0x03A8 or 0x03AF or 0x03B6 or 0x03BD or 0x03C4 or 0x03CB,
            _ => false
        };
        FieldScriptInputDemand[] forward = demands.Where(demand => demand.ProgramIdentity == vm.ProgramIdentity && demand is { Entity: 0, Priority: 7, Kind: FieldScriptInputKind.Pressed, Mask: 0x20, Mapped: false, AwaitingInput: true } && CurrentSource(demand)).ToArray();
        return forward.Length == 1 ? forward[0] : null;
    }
}
