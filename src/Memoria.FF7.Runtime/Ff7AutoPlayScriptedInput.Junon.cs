using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    // Reviewed native Junon chair input strategy (NG-116).
    internal const string JunonEscapeProgram = "F0869447B99A74D8601DEEAA41FA288C1091C5F2FA5B55ECF81E01C986F590E3";

    internal static Ff7AutoplayInput? ReadNativeJunonEscapeInput(FieldVmSnapshot vm, Ff7FieldProcess process, ReadOnlySpan<byte> script)
    {
        if (vm.ProgramIdentity != JunonEscapeProgram || vm.Entities.Length <= 3 || !process.PlayerLocked || !process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true, Entity: 3, Ladder: null, Jump: null } || vm.Entities[3] is not { Priority: 6, ModelBound: true, PartyCharacter: 2 } child || child.ProgramCounter is < 0x083A or > 0x09EF || child.SavedProgramCounters.Length != 8 || child.SavedProgramCounters[7] != 0x07B7 || child.SyncStates.Length != 8 || child.SyncOwners.Length != 8 || child.SyncStates[6] != FieldSyncState.Waiting || child.SyncOwners[6] != 0 || vm.Entities[0] is not { Priority: 7, ProgramCounter: 0x0523 } || script.Length <= 0x09EF || !script.Slice(0x0523, 3).SequenceEqual(new byte[] { 3, 3, 0xCC }))
        {
            return null;
        }
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        int stage = variables.ReadByte(FieldBank.MapByte, 0x13);
        if (variables.ReadWord(FieldBank.Persistent0Word, 0) != 1016 || stage > 6)
        {
            return null;
        }
        foreach (Ff7Dialogue dialogue in process.Dialogues.Where(dialogue => !dialogue.Completed))
        {
            if (dialogue is not { Window: 3, Text: 108, FirstChoice: null, LastChoice: null } || process.Windows.SingleOrDefault(window => window.Id == 3) is not { Style: 2, Flags: 1 } || vm.Entities[2] is not { Priority: 6, ProgramCounter: 0x074A } || !(vm.PendingRequests ?? []).Any(request => request.Id == dialogue.RequestId && request is { Entity: 2, Priority: 6, ProgramCounter: 0x074A, Command: FieldDialogueCommand { Window: 3, Text: 108, FirstChoice: null, LastChoice: null } }))
            {
                return null;
            }
        }
        int pc = child.ProgramCounter;
        FieldHostRequest? pending = (vm.PendingRequests ?? []).FirstOrDefault(request => request.Entity == 3 && request.Priority == 6 && request.ProgramCounter == pc);
        if (pending?.Command is FieldAnimation { Blocking: true } && script[pc] is 0xA3 or 0xBA or 0xBB or 0xBC || pending is null && script[pc] is 0xA3 or 0xBA or 0xBB or 0xBC)
        {
            return new(Held: 0, Run: false);
        }
        if (pending is not null)
        {
            return null;
        }
        bool poll = pc is 0x0864 or 0x088A or 0x08B3 or 0x08D9 or 0x0911 or 0x094D or 0x0989 or 0x09C5 || stage switch
        {
            0 => pc is 0x086A or 0x0871 or 0x087C or 0x0883,
            1 => pc is 0x0890 or 0x0897 or 0x08A5 or 0x08AC,
            2 => pc is 0x08B9 or 0x08C0 or 0x08C7 or 0x08CE,
            3 => pc is 0x08DF or 0x08E6 or 0x08EA or 0x0903 or 0x090A,
            4 => pc is 0x0917 or 0x091B or 0x092B or 0x0932 or 0x0936 or 0x0946,
            5 => pc is 0x0953 or 0x0974 or 0x097B or 0x0982,
            _ => pc is 0x098F or 0x0996 or 0x099D or 0x09BE
        };
        if (!poll)
        {
            return null;
        }
        ushort held = stage switch { 0 or 1 => 0x20, 2 => 0x10, 3 => 0x30, 4 => 0x90, 5 => 0x80, _ => 0x40 };
        return new(Held: held, Run: false);
    }
}
