using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayScriptedInputTests
{
    public static void Run()
    {
        FieldScriptInputDemand demand = new("program:0:1:0001", "program", 0, 1, 1, FieldScriptInputKind.Held, 0x220, true, true);
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new()).Held == 0x20, "Held demand did not choose one admitted native button.");
        demand = demand with { Kind = FieldScriptInputKind.Pressed };
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0x20, RawHeld: 0x20)).Held == 0, "KEYON must release an already held button before its fresh edge.");
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new()).Held == 0x20, "KEYON did not emit its press after release.");
        Check(!Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(Released: 0x20)), "A KEYON release setup tick must not consume the native event.");
        Check(!Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(Held: 0x20)), "A held button without a fresh edge must not consume KEYON admission.");
        Check(Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(Held: 0x20, Pressed: 0x20)), "A fresh mapped press failed its native entry gate.");
        Check(!Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(RawPressed: 0x20)), "Mapped entry admission used the raw pressed bank.");
        Ff7Line line = new(0, 1, 0, 0, 0, 100, 0, 0);
        Check(!Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineTouch, line, false), "An unobserved contact must not consume an entry pulse before native LINE dispatch.");
        Check(Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineTouch, line with { Touch = true }, false), "A saved native TOUCH request must receive its entry pulse before this tick's movement updates contacts.");
        Check(!Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineEnter, line with { Touch = true }, false), "Persistent TOUCH must not substitute for an ENTER edge.");
        Check(Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineEnter, line with { Enter = true }, false), "Native ENTER edge lost its entry pulse.");
        demand = demand with { Kind = FieldScriptInputKind.Released };
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new()).Held == 0x20, "KEYOFF must establish a held button before its release edge.");
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0x20, RawHeld: 0x20)).Held == 0, "KEYOFF failed to emit a distinct release tick.");
        demand = demand with { Mapped = false };
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0x20, RawHeld: 0)).Held == 0x20, "Raw KEYOFF used the mapped held bank.");
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0, RawHeld: 0x20)).Held == 0, "Raw KEYOFF ignored its native held bank.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
