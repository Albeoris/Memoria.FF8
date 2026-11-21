using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    private const string ExcavationProgram = "CB5DD0DE1FCFDBC2FB36AA8B960EEB05A6E46AED03B256712851B87434A5ADDE";

    internal static bool IsExcavationBombInput(FieldVmSnapshot vm, Ff7FieldProcess process, Ff7Dialogue prompt, FieldHostRequest request, FieldScriptInputDemand demand)
    {
        if (vm.ProgramIdentity != ExcavationProgram || demand.ProgramIdentity != ExcavationProgram || demand.Entity != 0 || demand.Priority != 7 || demand.ProgramCounter != 0x06C9 || demand.Kind != FieldScriptInputKind.Pressed || demand.Mapped || demand.Mask != 0x80 || !demand.AwaitingInput || vm.Entities.Length <= 2 || vm.MapVariables.Length <= 0x2B || vm.PersistentVariables.Length <= 0xEB || vm.MapVariables[0x0C] != 2 || vm.MapVariables[0x2B] != 0 || vm.PersistentVariables[0xEB] != 1)
        {
            return false;
        }
        if (process.PlayerLocked || !process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || prompt.Completed || !prompt.AwaitingInput || prompt.FirstChoice is not null || prompt.LastChoice is not null || prompt.Window != 3 || prompt.Text != 29 || request.Id != prompt.RequestId || request.Entity != 1 || request.Priority != 6 || request.ProgramCounter != 0x06F8 || request.Command != new FieldDialogueCommand(3, 29) || !(vm.PendingRequests ?? []).Contains(request))
        {
            return false;
        }
        Ff7Window? window = process.Windows.SingleOrDefault(window => window.Id == prompt.Window);
        if (window is not { Style: 2 } || (window.Flags & 1) == 0)
        {
            return false;
        }
        FieldEntitySnapshot director = vm.Entities[0];
        FieldEntitySnapshot message = vm.Entities[1];
        FieldEntitySnapshot worker = vm.Entities[2];
        return director.Priority == 7 && director.ProgramCounter is 0x06C3 or 0x06C9 or 0x06D0 && message.Priority == request.Priority && message.ProgramCounter == request.ProgramCounter && worker.Priority == 7 && worker.SyncStates.Length > 6 && worker.SyncOwners.Length > 6 && worker.SavedProgramCounters.Length > 6 && worker.SyncStates[6] == FieldSyncState.None && worker.SyncOwners[6] == -1 && worker.SavedProgramCounters[6] == 0;
    }
}
