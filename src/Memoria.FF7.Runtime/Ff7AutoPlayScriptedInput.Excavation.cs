using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    private const string ExcavationProgram = "CB5DD0DE1FCFDBC2FB36AA8B960EEB05A6E46AED03B256712851B87434A5ADDE";

    internal static bool IsExcavationProgram(string programIdentity)
    {
        return programIdentity == ExcavationProgram;
    }

    internal static int? ReadExcavationDigTriangle(FieldVmSnapshot vm, Ff7FieldProcess process)
    {
        if (!IsExcavationProgram(vm.ProgramIdentity) || vm.Entities.Length <= 2 || vm.MapVariables.Length <= 0x2B || vm.PersistentVariables.Length <= 0xEB || vm.MapVariables[0x0C] != 1 || vm.MapVariables[0x2B] != 0 || vm.PersistentVariables[0xEB] != 1 || vm.PersistentVariables[0xEA] != 0 || (vm.PersistentVariables[0xE7] & 8) != 0 || process.PlayerLocked || !process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true })
        {
            return null;
        }
        FieldEntitySnapshot director = vm.Entities[0];
        FieldEntitySnapshot worker = vm.Entities[2];
        if (director.Priority != 7 || director.ProgramCounter is not (0x06C3 or 0x06C9 or 0x06D0) || worker.Priority != 7 || worker.SyncStates.Length <= 6 || worker.SyncOwners.Length <= 6 || worker.SavedProgramCounters.Length <= 6 || worker.SyncStates[6] != FieldSyncState.None || worker.SyncOwners[6] != -1 || worker.SavedProgramCounters[6] != 0)
        {
            return null;
        }
        int triangle = vm.MapVariables[0x10] | vm.MapVariables[0x11] << 8;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        int character = new FieldVariables(vm.PersistentVariables, vm.MapVariables).ReadByte(FieldBank.Persistent1Byte, 9);
        int queried = character < 9 ? Array.FindIndex(vm.Entities, entity => entity.PartyCharacter == character) : -1;
        if (player is not { EntityBound: true } || queried != player.Entity)
        {
            return null;
        }
        Ff7Actor[] markers = process.Actors.Where(actor => actor.EntityBound && actor.Entity == 6 && !actor.PlayerControlled).ToArray();
        if (markers.Length != 1 || markers[0].Triangle != triangle || markers[0].CollisionEnabled)
        {
            return null;
        }
        for (int address = 0x12; address <= 0x1C; address += 2)
        {
            if ((vm.MapVariables[address] | vm.MapVariables[address + 1] << 8) == triangle)
            {
                return null;
            }
        }
        return triangle;
    }

    internal static bool IsExcavationBombContinuation(FieldVmSnapshot vm, Ff7FieldProcess process)
    {
        if (!IsExcavationProgram(vm.ProgramIdentity) || vm.Entities.Length <= 2 || vm.MapVariables.Length <= 0x2B || vm.PersistentVariables.Length <= 0xEB || vm.PersistentVariables[0xEB] != 1 || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true })
        {
            return false;
        }
        FieldEntitySnapshot director = vm.Entities[0];
        FieldEntitySnapshot worker = vm.Entities[2];
        if (director.Priority != 7 || director.ProgramCounter != 0x06CD || worker.Priority != 6 || worker.SyncStates.Length <= 6 || worker.SyncOwners.Length <= 6 || worker.SavedProgramCounters.Length <= 7 || worker.SyncStates[6] != FieldSyncState.Waiting || worker.SyncOwners[6] != 0 || worker.SavedProgramCounters[7] != 0x06FD)
        {
            return false;
        }
        int phase = vm.MapVariables[0x0C];
        return phase == 2 && worker.ProgramCounter is >= 0x07EE and <= 0x0895 || phase == 1 && worker.ProgramCounter is >= 0x0895 and <= 0x0AB4;
    }

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
