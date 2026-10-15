using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static bool IsSemanticSpatialFacingOpcode(Jsm.Opcode opcode)
    {
        return opcode is Jsm.Opcode.DIR or Jsm.Opcode.DIRA or Jsm.Opcode.PDIRA or Jsm.Opcode.DIRP
            or Jsm.Opcode.CTURN or Jsm.Opcode.CTURNL or Jsm.Opcode.CTURNR or Jsm.Opcode.LTURN or Jsm.Opcode.LTURNL or Jsm.Opcode.LTURNR
            or Jsm.Opcode.PCTURN or Jsm.Opcode.PLTURN or Jsm.Opcode.Unknown7 or Jsm.Opcode.Unknown8 or Jsm.Opcode.Unknown9
            or Jsm.Opcode.FACEDIR or Jsm.Opcode.RFACEDIR or Jsm.Opcode.FACEDIRP or Jsm.Opcode.RFACEDIRP or Jsm.Opcode.FACEDIRA or Jsm.Opcode.RFACEDIRA
            or Jsm.Opcode.FACEDIROFF or Jsm.Opcode.RFACEDIROFF or Jsm.Opcode.FACEDIRLIMIT or Jsm.Opcode.FACEDIRINIT or Jsm.Opcode.FACEDIRSYNC;
    }

    private static int SemanticSpatialFacingOperandCount(Jsm.Opcode opcode)
    {
        return opcode switch
        {
            Jsm.Opcode.FACEDIRINIT or Jsm.Opcode.FACEDIRSYNC => 0,
            Jsm.Opcode.DIR or Jsm.Opcode.DIRA or Jsm.Opcode.PDIRA or Jsm.Opcode.FACEDIROFF or Jsm.Opcode.RFACEDIROFF => 1,
            Jsm.Opcode.CTURN or Jsm.Opcode.CTURNL or Jsm.Opcode.CTURNR or Jsm.Opcode.LTURN or Jsm.Opcode.LTURNL or Jsm.Opcode.LTURNR
                or Jsm.Opcode.PCTURN or Jsm.Opcode.PLTURN or Jsm.Opcode.Unknown7 or Jsm.Opcode.Unknown8 or Jsm.Opcode.Unknown9
                or Jsm.Opcode.FACEDIRP or Jsm.Opcode.RFACEDIRP or Jsm.Opcode.FACEDIRA or Jsm.Opcode.RFACEDIRA => 2,
            Jsm.Opcode.DIRP or Jsm.Opcode.FACEDIRLIMIT => 3,
            Jsm.Opcode.FACEDIR or Jsm.Opcode.RFACEDIR => 4,
            _ => throw new NotSupportedException($"{opcode} has no implemented semantic facing operand contract.")
        };
    }

    /// <summary>Completed-visit replay intentionally ignores facing presentation while preserving the current VM's operand consumption and result registers.</summary>
    private int[] ExecuteSemanticSpatialFacing(Fiber fiber, JsmOperation operation)
    {
        int count = SemanticSpatialFacingOperandCount(operation.Opcode);
        if (fiber.Stack.Count < count || fiber.StackExpressions.Count < count)
        {
            throw new NotSupportedException($"{operation.Opcode} requires its original {count} facing operands.");
        }
        NativeScalarValue[] operands = fiber.Stack.Capture().Take(count).Reverse().ToArray();
        int[] arguments = new int[count];
        for (int index = 0; index < count; index++)
        {
            bool word = operation.Opcode is Jsm.Opcode.FACEDIR or Jsm.Opcode.RFACEDIR or Jsm.Opcode.FACEDIROFF or Jsm.Opcode.RFACEDIROFF
                || index == count - 1 && operation.Opcode is (Jsm.Opcode.FACEDIRP or Jsm.Opcode.RFACEDIRP or Jsm.Opcode.FACEDIRA or Jsm.Opcode.RFACEDIRA);
            NativeScalarValue value = operands[index];
            arguments[index] = value.IsExact ? value.RequireExact() : value.ToWidth(word ? 2 : 4).RequireExact();
        }
        string[] expressions = fiber.StackExpressions.Take(count).Reverse().ToArray();
        for (int index = 0; index < count; index++)
        {
            fiber.Stack.PopValue();
            fiber.StackExpressions.Pop();
        }
        fiber.LastExpressions = expressions;
        ActorState actor = actors[fiber.ObjectId];
        actor.RotationTarget = null;
        actor.RotationFrames = 0;
        actor.RotationTotalFrames = 0;
        actor.RotationUsesNativeCosine = false;
        actor.RotationUsesNativeLinear = false;
        actor.FaceTargetPoint = null;
        actor.FaceReturning = false;
        actor.FaceDuration = 0;
        actor.FaceProgress = 0;
        actor.FirstControlRotation = null;
        if (fiber.WaitReason is WaitReason.Rotation or WaitReason.FaceDirection or WaitReason.NativeFaceSync)
        {
            fiber.WaitReason = WaitReason.None;
        }
        return arguments;
    }
}
