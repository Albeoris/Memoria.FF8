using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    private const string ClockProgram = "990BE7C79174E0B781F19BECE4C986E452CD15247A975847624305ECC7FD7669";

    private bool TryChooseClockInput(IReadOnlyList<FieldScriptInputDemand> demands, out Ff7AutoplayInput input)
    {
        input = default;
        FieldScriptInputDemand[] controls = demands.Where(demand => demand.ProgramIdentity == ClockProgram && demand.Entity == 25 && demand.Priority == 6 && demand.AwaitingInput && !demand.Mapped && demand.Kind == FieldScriptInputKind.Pressed).ToArray();
        if (controls.Length == 0)
        {
            return false;
        }
        ushort? destination = session.AutoPlayState?.DestinationField;
        short story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        if (story is < 618 or >= 627 || destination is not null and not 610)
        {
            return false;
        }
        FieldScriptInputDemand? stop = controls.SingleOrDefault(demand => demand.ProgramCounter == 0x1F01 && demand.Mask == 0x20);
        if (stop is not null)
        {
            CurrentDemand = stop;
            Ff7AutoplayInput projected = SelectInput(stop, session.Machine.Input);
            if (projected.Held == 0)
            {
                return true;
            }
            input = PredictClockStop() ? projected : new(Run: false);
            return true;
        }
        bool aligned = IsClockStoryBridge(ReadClockHand(session, 0xE2), ReadClockHand(session, 0xE4));
        ushort button = aligned ? (ushort)0x40 : (ushort)0x20;
        CurrentDemand = controls.FirstOrDefault(demand => demand.Mask == button);
        if (CurrentDemand is null)
        {
            throw new NotSupportedException("The clock has an unsupported native control phase for the requested bridge.");
        }
        input = SelectInput(CurrentDemand, session.Machine.Input);
        return true;
    }

    internal static bool IsClockStoryBridge(int longHand, int shortHand) => longHand == 6 && shortHand == 10 || longHand == 10 && shortHand == 6;

    private static int ReadClockHand(Ff7Session source, ushort address)
    {
        int value = source.Machine.Variables.ReadWord(FieldBank.Persistent1Word, address);
        if (value is < 0 or >= 12)
        {
            throw new InvalidDataException("A native clock hand is outside its twelve positions.");
        }
        return value;
    }

    private bool PredictClockStop()
    {
        using Ff7Session branch = session.ForkAutoPlayNavigation();
        bool stopped = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            FieldScriptInputDemand[] controls = branch.Machine.ReadCurrentInputDemands().Where(demand => demand.Entity == 25 && demand.Priority == 6 && demand.AwaitingInput && !demand.Mapped && demand.Kind == FieldScriptInputKind.Pressed).ToArray();
            if (controls.Any(demand => demand.Mask == 0x40) && controls.Any(demand => demand.Mask == 0x20))
            {
                stopped = true;
                return default;
            }
            FieldScriptInputDemand? stop = controls.SingleOrDefault(demand => demand.ProgramCounter == 0x1F01 && demand.Mask == 0x20);
            if (stop is not null)
            {
                return SelectInput(stop, branch.Machine.Input);
            }
            if (controls.Length != 0)
            {
                throw new NotSupportedException("The clock stop continuation requested an unsupported input phase.");
            }
            return default;
        }
        Ff7AutoPlayController controller = new(branch, Choose);
        controller.Start();
        while (!stopped && controller.Running)
        {
            controller.Advance(1);
            if (branch.MapId != session.MapId || branch.Battle is not null || branch.World is not null)
            {
                throw new NotSupportedException("The clock stop continuation left its native room contract.");
            }
        }
        if (!stopped || controller.Error is not null)
        {
            throw new NotSupportedException($"The clock stop continuation did not settle: {controller.Error}");
        }
        return IsClockStoryBridge(ReadClockHand(branch, 0xE2), ReadClockHand(branch, 0xE4));
    }
}
