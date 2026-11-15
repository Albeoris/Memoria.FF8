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
        FieldScriptInputDemand direction = demand with { Identity = "direction", Kind = FieldScriptInputKind.Held, Mapped = false, Mask = 0x1000 };
        FieldScriptInputDemand confirm = direction with { Identity = "confirm", Mapped = true, Mask = 0x220 };
        FieldScriptInputDemand cancel = confirm with { Identity = "cancel", Mask = 0x240 };
        Check(Ff7AutoPlayScriptedInput.SelectDemand([direction, confirm, cancel]) == cancel, "A native menu loop selected navigation or Confirm before its mapped Cancel gate.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([direction, confirm]) == confirm, "A native menu loop selected navigation before mapped Confirm.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel with { AwaitingInput = false }, direction, confirm]) == confirm, "An unproved Cancel branch displaced a proved input wait.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel with { Mapped = false }, confirm]) == confirm, "A raw Cancel-shaped bitmask displaced mapped Confirm.");
        FieldScriptInputDemand earlierOwner = direction with { Entity = 0, Priority = 0 };
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel, earlierOwner]) == earlierOwner, "Button preference displaced native script priority.");
        earlierOwner = direction with { Entity = 0 };
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel with { Entity = 1 }, earlierOwner]) == earlierOwner, "Button preference displaced native entity ordering.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([direction, direction with { Identity = "second-direction", Mask = 0x2000 }]) == direction, "Equivalent navigation gates lost stable source ordering.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([]) is null && Ff7AutoPlayScriptedInput.SelectDemand([cancel with { AwaitingInput = false }]) is null, "No proved wait fabricated an autoplay button demand.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
