using System.Reflection;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void AutoPlayPositionGateContract()
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations =
        [
            (Jsm.Opcode.LBL, 0),
            (Jsm.Opcode.PSHM_B, 1028),
            (Jsm.Opcode.PSHN_L, 0),
            (Jsm.Opcode.CAL, 6),
            (Jsm.Opcode.JPF, 19),
            (Jsm.Opcode.GETINFO, 0),
            (Jsm.Opcode.PSHI_L, 0),
            (Jsm.Opcode.PSHN_L, 1300),
            (Jsm.Opcode.CAL, 5),
            (Jsm.Opcode.CAL, 9),
            (Jsm.Opcode.JPF, 12),
            (Jsm.Opcode.PSHI_L, 1),
            (Jsm.Opcode.PSHN_L, 1425),
            (Jsm.Opcode.CAL, 5),
            (Jsm.Opcode.CAL, 7),
            (Jsm.Opcode.JPF, 7),
            (Jsm.Opcode.PSHI_L, 2),
            (Jsm.Opcode.PSHN_L, 4796),
            (Jsm.Opcode.CAL, 6),
            (Jsm.Opcode.JPF, 3),
            (Jsm.Opcode.PSHN_L, 1),
            (Jsm.Opcode.POPM_B, 1028),
            (Jsm.Opcode.JMP, -21),
            (Jsm.Opcode.HALT, 0),
            (Jsm.Opcode.RET, 8)
        ];
        MethodInfo parser = typeof(EventEngine).GetMethod("TryReadAutoPlayPositionGate", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Parse((Jsm.Opcode Opcode, int Parameter)[] code, int position, out NativePlayerPositionGate gate)
        {
            JsmProgram program = JsmProgram.Read(SyntheticProgram(code));
            object?[] args = [program, program.Objects[0].Scripts[0], position, 5, 7, null];
            bool result = (bool)parser.Invoke(null, args)!;
            gate = (NativePlayerPositionGate)args[5]!;
            return result;
        }
        Require(Parse(operations, 22, out NativePlayerPositionGate compound) && compound.OwnerObjectId == 7 && compound.Constraints.Count == 3, "A compound GETINFO loop lost its owner or common retry edge.");
        Require(compound.Admits(-1400, -1400, 4796) && !compound.Admits(-1300, -1400, 4796) && !compound.Admits(-1400, -1425, 4796) && !compound.Admits(-1400, -1400, 4795), "Compound native position constraints lost strict bounds or exact height.");
        Require(compound.Admits(-1400, -1400, 4796, 20) && !compound.Admits(-1310, -1400, 4796, 20), "A walking margin changed equality or failed to tighten a strict boundary.");
        NativePositionConstraint negative = new(0, NativePositionComparison.LessThan, -1);
        Require(!negative.Admits(-1.9f, 0, 0) && negative.Admits(-2.1f, 0, 0), "Position admission no longer uses native signed truncation toward zero.");
        var divergent = operations.ToArray();
        divergent[15] = (Jsm.Opcode.JPF, 6);
        Require(!Parse(divergent, 10, out _), "A conjunction with divergent failure destinations was admitted.");
        var unsupported = operations.ToArray();
        unsupported[14] = (Jsm.Opcode.CAL, 8);
        Require(!Parse(unsupported, 10, out _), "An unsupported position comparison was admitted.");
        Require(!Parse(operations, 23, out _) && !Parse(operations, 0, out _), "A position poll was discovered outside its active loop PCs.");
        var duplicate = operations.ToArray();
        duplicate[11] = (Jsm.Opcode.PSHI_L, 0);
        Require(!Parse(duplicate, 10, out _), "A duplicate coordinate register was admitted as a compound region.");
        (Jsm.Opcode Opcode, int Parameter)[] simple = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.GETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.CAL, 9), (Jsm.Opcode.JPF, 3), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.JMP, -7), (Jsm.Opcode.RET, 8)];
        JsmProgram old = JsmProgram.Read(SyntheticProgram(simple));
        object?[] oldArgs = [old, old.Objects[0].Scripts[0], 3, 1, 2, null];
        Require((bool)parser.Invoke(null, oldArgs)! && ((NativePlayerPositionGate)oldArgs[5]!).Admits(11, 0), "The original single-coordinate player poll was lost.");
    }
}
