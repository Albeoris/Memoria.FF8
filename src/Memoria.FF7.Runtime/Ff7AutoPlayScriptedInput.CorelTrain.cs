using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    // Reviewed native Corel train lever input strategy (NG-117).
    internal const string CorelTrainProgram = "E9C23833A74BC5D9419A218ACEDAB92664C5BBFB839CC5577AB17960C1FCB7EB";

    internal static Ff7AutoplayInput? ReadNativeCorelTrainInput(FieldVmSnapshot vm, Ff7FieldProcess process, ReadOnlySpan<byte> script, FieldInputState input)
    {
        if (vm.ProgramIdentity != CorelTrainProgram || vm.Entities.Length <= 13 || !process.PlayerLocked || !process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true, Entity: 11, Model: 6, Ladder: null, Jump: null } || vm.Entities[11] is not { Priority: 7, ModelBound: true, PartyCharacter: 8 } worker || (vm.PendingRequests ?? []).Any(request => request.Entity == 11 && request.Priority == 7))
        {
            return null;
        }
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        if (variables.ReadWord(FieldBank.Persistent0Word, 0) != 1110)
        {
            return null;
        }
        int phase = variables.ReadByte(FieldBank.MapByte, 0x16);
        int pc = worker.ProgramCounter;
        ushort chord;
        if (phase == 4 && pc is 0x08B4 or 0x08B8 or 0x08D3 or 0x08D7)
        {
            chord = 0x1010;
        }
        else if (phase == 2 && pc is 0x09F6 or 0x09FA or 0x0A15 or 0x0A19)
        {
            chord = 0x4040;
        }
        else
        {
            return null;
        }
        ushort mask = pc switch { 0x08B4 or 0x09F6 => 0x1000, 0x08B8 or 0x09FA => 0x10, 0x08D3 or 0x0A15 => 0x4000, _ => 0x40 };
        if (pc + 3 >= script.Length || script[pc] != 0x31 || script[pc + 1] != (byte)mask || script[pc + 2] != (byte)(mask >> 8))
        {
            return null;
        }
        foreach (Ff7Dialogue dialogue in process.Dialogues.Where(dialogue => !dialogue.Completed))
        {
            (int Entity, int Pc, int Text, int Style)? producer = dialogue.Window switch { 1 => (4, 0x0622, 222, 2), 2 => (13, 0x0C59, 207, 1), _ => null };
            if (producer is not { } owner || dialogue.Text != owner.Text || dialogue.FirstChoice is not null || dialogue.LastChoice is not null || process.Windows.SingleOrDefault(window => window.Id == dialogue.Window) is not { Flags: 1 } window || window.Style != owner.Style || vm.Entities[owner.Entity].Priority != 6 || vm.Entities[owner.Entity].ProgramCounter != owner.Pc || !(vm.PendingRequests ?? []).Any(request => request.Id == dialogue.RequestId && request.Entity == owner.Entity && request.Priority == 6 && request.ProgramCounter == owner.Pc && request.Command is FieldDialogueCommand command && command.Window == dialogue.Window && command.Text == owner.Text && command.FirstChoice is null && command.LastChoice is null))
            {
                return null;
            }
        }
        return new(Held: (input.RawHeld & chord) == 0 ? chord : (ushort)0, Run: false);
    }
}
