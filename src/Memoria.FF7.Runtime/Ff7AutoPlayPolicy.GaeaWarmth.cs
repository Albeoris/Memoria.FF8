using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native cliff warming input (NG-109).
    private const string GaeaFirstCliffProgram = "5CA6E94C40B2BE8DA121FF1E0A758A5076BA09BDE67DDA155E440EA01000B61C";

    private const string GaeaSecondCliffProgram = "97E4CD1C842F666BDB1E727CF2F000FE595A11409ED2A51116ADB85DD086F741";

    internal static bool IsNativeGaeaWarmthProgram(string programIdentity)
    {
        return programIdentity is GaeaFirstCliffProgram or GaeaSecondCliffProgram;
    }

    internal static bool IsNativeGaeaWarmthDemand(FieldScriptInputDemand demand)
    {
        return demand is { Entity: 4, Priority: 6, Mapped: false, Kind: FieldScriptInputKind.Pressed, Mask: 0x80 } && (demand.ProgramIdentity == GaeaFirstCliffProgram && demand.ProgramCounter == 0x740 || demand.ProgramIdentity == GaeaSecondCliffProgram && demand.ProgramCounter == 0x976);
    }

    internal static Ff7AutoplayInput? ReadNativeGaeaWarmthInput(FieldVmSnapshot vm, Ff7FieldProcess process, FieldVariables variables, FieldInputState previous)
    {
        bool first = vm.ProgramIdentity == GaeaFirstCliffProgram;
        int start = first ? 0x73A : 0x970;
        int end = first ? 0x770 : 0x9A6;
        if (!IsNativeGaeaWarmthProgram(vm.ProgramIdentity) || !vm.Initialized || vm.Entities.Length <= 4 || vm.Entities[4] is not { Priority: 6 } worker || worker.ProgramCounter < start || worker.ProgramCounter > end || worker.SyncStates.Length <= 6 || worker.SyncStates[6] != FieldSyncState.None || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed && !IsNativeGaeaTemperatureHud(vm, process, dialogue, first)))
        {
            return null;
        }
        int temperature = variables.ReadWord(FieldBank.MapWord, first ? (ushort)9 : (ushort)8);
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (temperature is < 27 or >= 38 || (variables.ReadByte(FieldBank.MapByte, first ? (byte)0x0D : (byte)0x0E) & 1) != 0 || (variables.ReadByte(FieldBank.MapByte, first ? (byte)0x0E : (byte)0x0F) & 2) != 0 || player is not { EntityBound: true, Ladder: null, Jump: null } || player.Entity != (first ? 17 : 22) || process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed))
        {
            return null;
        }
        ushort held = (ushort)(((previous.Held | previous.RawHeld) & 0x80) == 0 ? 0x80 : 0);
        return new(Held: held, Run: false);
    }

    private static bool IsNativeGaeaTemperatureHud(FieldVmSnapshot vm, Ff7FieldProcess process, Ff7Dialogue dialogue, bool first)
    {
        int pc = first ? 0x725 : 0x95B;
        int text = first ? 1 : 5;
        return dialogue is { Window: 1, FirstChoice: null, LastChoice: null } && dialogue.Text == text && process.Windows.Any(window => window is { Id: 1, Style: 0, Flags: 1 }) && vm.Entities.Length > 2 && vm.Entities[2] is { Priority: 6 } display && display.ProgramCounter == pc && vm.PendingRequests?.Any(request => request.Id == dialogue.RequestId && request is { Entity: 2, Priority: 6 } && request.ProgramCounter == pc && request.Command is FieldDialogueCommand { Window: 1, FirstChoice: null, LastChoice: null } command && command.Text == text) == true;
    }
}
