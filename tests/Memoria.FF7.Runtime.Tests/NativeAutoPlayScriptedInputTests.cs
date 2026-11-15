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
