using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native cliff warming input (NG-109).
    private sealed record GaeaWarmthProfile(string Program, int WorkerStart, int WorkerEnd, int KeyPc, ushort Temperature, byte Busy, byte Cold, int Player, int HudPc, int HudText);

    private static readonly GaeaWarmthProfile[] GaeaWarmthProfiles =
    [
        new("5CA6E94C40B2BE8DA121FF1E0A758A5076BA09BDE67DDA155E440EA01000B61C", 0x73A, 0x770, 0x740, 9, 0x0D, 0x0E, 17, 0x725, 1),
        new("97E4CD1C842F666BDB1E727CF2F000FE595A11409ED2A51116ADB85DD086F741", 0x970, 0x9A6, 0x976, 8, 0x0E, 0x0F, 22, 0x95B, 5),
        new("B34260E0B629A52DD758E497A823D077296D9890CA92CBE1D26C3A2683CF8E05", 0x894, 0x8CA, 0x89A, 8, 0x0D, 0x0E, 20, 0x87F, 3)
    ];

    private static GaeaWarmthProfile? ReadGaeaWarmthProfile(string programIdentity)
    {
        return GaeaWarmthProfiles.FirstOrDefault(profile => profile.Program == programIdentity);
    }

    internal static bool IsNativeGaeaWarmthProgram(string programIdentity)
    {
        return ReadGaeaWarmthProfile(programIdentity) is not null;
    }

    internal static bool IsNativeGaeaWarmthDemand(FieldScriptInputDemand demand)
    {
        return demand is { Entity: 4, Priority: 6, Mapped: false, Kind: FieldScriptInputKind.Pressed, Mask: 0x80 } && ReadGaeaWarmthProfile(demand.ProgramIdentity) is { } profile && demand.ProgramCounter == profile.KeyPc;
    }

    internal static Ff7AutoplayInput? ReadNativeGaeaWarmthInput(FieldVmSnapshot vm, Ff7FieldProcess process, FieldVariables variables, FieldInputState previous)
    {
        GaeaWarmthProfile? profile = ReadGaeaWarmthProfile(vm.ProgramIdentity);
        if (profile is null || !vm.Initialized || vm.Entities.Length <= 4 || vm.Entities[4] is not { Priority: 6 } worker || worker.ProgramCounter < profile.WorkerStart || worker.ProgramCounter > profile.WorkerEnd || worker.SyncStates.Length <= 6 || worker.SyncStates[6] != FieldSyncState.None || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed && !IsNativeGaeaTemperatureHud(vm, process, dialogue, profile)))
        {
            return null;
        }
        int temperature = variables.ReadWord(FieldBank.MapWord, profile.Temperature);
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (temperature is < 27 or >= 38 || (variables.ReadByte(FieldBank.MapByte, profile.Busy) & 1) != 0 || (variables.ReadByte(FieldBank.MapByte, profile.Cold) & 2) != 0 || player is not { EntityBound: true, Ladder: null, Jump: null } || player.Entity != profile.Player || process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed))
        {
            return null;
        }
        ushort held = (ushort)(((previous.Held | previous.RawHeld) & 0x80) == 0 ? 0x80 : 0);
        return new(Held: held, Run: false);
    }

    private static bool IsNativeGaeaTemperatureHud(FieldVmSnapshot vm, Ff7FieldProcess process, Ff7Dialogue dialogue, GaeaWarmthProfile profile)
    {
        return dialogue is { Window: 1, FirstChoice: null, LastChoice: null } && dialogue.Text == profile.HudText && process.Windows.Any(window => window is { Id: 1, Style: 0, Flags: 1 }) && vm.Entities.Length > 2 && vm.Entities[2] is { Priority: 6 } display && display.ProgramCounter == profile.HudPc && vm.PendingRequests?.Any(request => request.Id == dialogue.RequestId && request is { Entity: 2, Priority: 6 } && request.ProgramCounter == profile.HudPc && request.Command is FieldDialogueCommand { Window: 1, FirstChoice: null, LastChoice: null } command && command.Text == profile.HudText) == true;
    }
}
