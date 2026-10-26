using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void JsmResourceValidationContract()
    {
        JsmResourceValidationResult Validate(int messages, params (Jsm.Opcode Opcode, int Parameter)[] body)
        {
            (Jsm.Opcode, int)[] operations = new[] { (Jsm.Opcode.LBL, 0) }.Concat(body).Append((Jsm.Opcode.RET, 0)).ToArray();
            return JsmResourceValidation.Validate(SyntheticObjects([0], [operations]), messages);
        }

        Jsm.Opcode unsupported = (Jsm.Opcode)ushort.MaxValue;
        Require(!Enum.IsDefined(unsupported) && !EventEngine.SupportsInstructionOpcode(unsupported), "The unsupported-opcode fixture must be an encodable unknown operation.");
        Require(Validate(0, (unsupported, 0)).Errors.Any(error => error.Code == "unsupported-opcode"), "Interpreter support must reject an unknown encoded operation.");
        Require(Validate(0, (Jsm.Opcode.NOP, 0)).IsValid, "An empty supported script must validate without a text table.");
        foreach ((Jsm.Opcode opcode, int count) in new[] { (Jsm.Opcode.MES, 2), (Jsm.Opcode.MESW, 2), (Jsm.Opcode.AMES, 4), (Jsm.Opcode.AMESW, 4), (Jsm.Opcode.RAMESW, 4), (Jsm.Opcode.ASK, 6), (Jsm.Opcode.AASK, 8) })
        {
            (Jsm.Opcode, int)[] operands = Enumerable.Range(0, count).Select(index => (Jsm.Opcode.PSHN_L, index == 1 ? 1 : 0)).Append((opcode, 0)).ToArray();
            Require(Validate(2, operands).IsValid, $"{opcode} has a valid static message reference.");
            Require(Validate(1, operands).Errors.Any(error => error.Code == "message-out-of-range"), $"{opcode} must reject an MSD reference at the selected count boundary.");
        }
        Require(Validate(1, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0)).Errors.Any(error => error.Code == "operand-stack-underflow"), "A proved message operand underflow must be rejected.");
        Require(Validate(1, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.MES, 0)).Errors.Any(error => error.Code == "message-out-of-range"), "Negative constant message references are invalid.");
        JsmResourceValidationResult dynamic = Validate(1, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHM_W, 10), (Jsm.Opcode.MES, 0));
        Require(dynamic.IsValid && dynamic.DynamicMessageReferences == 1, "Dynamic original message IDs must remain explicit uncertainty, not be rejected.");
        JsmResourceValidationResult folded = Validate(2, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.CAL, 1), (Jsm.Opcode.MES, 0));
        Require(folded.Errors.Any(error => error.Code == "message-out-of-range"), "Constant text arithmetic must use the interpreter's native CAL contract.");
        Require(Validate(1, (Jsm.Opcode.CAL, 18)).Errors.Any(error => error.Code == "invalid-calculation"), "Enum-known CAL must validate its actual native selector.");
        foreach (int division in new[] { 3, 4 })
        {
            Require(Validate(0, (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, division)).Errors.Any(error => error.Code == "invalid-calculation" && error.Operation == 3), "Native division and remainder must reject a proved zero divisor.");
            Require(Validate(0, (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 31), (Jsm.Opcode.CAL, 17), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.CAL, division)).Errors.Any(error => error.Code == "invalid-calculation"), "Native signed division overflow must also reject a remainder-only calculation.");
            Require(Validate(0, (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHM_W, 10), (Jsm.Opcode.CAL, division)).IsValid, "A dynamic divisor must remain a runtime check.");
        }
        Require(Validate(0, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.REQ, 1)).Errors.Any(error => error.Code == "invalid-object-reference"), "Static request objects must refer to the selected object table.");
        JsmResourceValidationResult unknown = Validate(1, (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.MES, 0));
        Require(unknown.IsValid && unknown.UnprovenStackOperations > 0 && unknown.DynamicMessageReferences == 1, "Unmodeled native effects must widen stack knowledge without invented operands.");
        JsmResourceValidationResult branch = Validate(1, (Jsm.Opcode.PSHM_W, 10), (Jsm.Opcode.JPF, 5), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0), (Jsm.Opcode.JMP, 4), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MES, 0));
        Require(branch.Errors.Any(error => error.Code == "message-out-of-range" && error.Operation == 9), "Both possible original branches must be inspected.");
        JsmResourceValidationResult merged = Validate(1, (Jsm.Opcode.PSHM_W, 10), (Jsm.Opcode.JPF, 4), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JMP, 3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHM_W, 11), (Jsm.Opcode.MES, 0));
        Require(merged.IsValid && merged.DynamicMessageReferences == 1, "Branch joins must not turn a dynamic message ID into a constant.");
        JsmResourceValidationResult unreachable = Validate(0, (Jsm.Opcode.JMP, 2), (unsupported, 0));
        Require(unreachable.IsValid && unreachable.UnreachableUnsupportedOperations == 1, "Unreachable unsupported words must be counted separately from executable instructions.");
        JsmResourceValidationResult constantDead = Validate(0, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JPF, 2), (unsupported, 0));
        Require(constantDead.IsValid && constantDead.UnreachableUnsupportedOperations == 1, "Proved constant-dead unsupported words must also be counted as unreachable.");
        byte[] call = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.GJMP, 1), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.RET, 9)]]);
        Require(JsmResourceValidation.Validate(call, 0).IsValid, "Native global result frames must not be analyzed as ordinary empty stacks.");
        try
        {
            _ = Validate(0, (Jsm.Opcode.JMP, 100));
            throw new InvalidOperationException("Structural branch validation was bypassed.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine("JSM selected-resource preflight: interpreter support, structural branches, static text/stack/object references and dynamic uncertainty passed.");
    }
}
