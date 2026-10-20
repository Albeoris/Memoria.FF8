using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeGlobalJumpFrame(int CallOperation, int TargetOperation, int ReturnOperation, int OperandBase, bool Entered);

public sealed partial class EventEngine
{
    private string? UnsupportedNativeGlobalJump(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.GJMP)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || program is null || (uint)operation.Parameter >= program.NativeScriptPointers.Count)
        {
            return "Native GJMP requires the original validated global script pointer table and runtime profile";
        }
        NativeScriptPointer entry = program.NativeScriptPointers[operation.Parameter];
        bool admitted = (entry.RawPointer & 0x8000) != 0 || Scene.NativeActorObjectIds.Any(id => operation.Parameter >= program.Objects[id].Label && operation.Parameter <= program.Objects[id].Label + program.Objects[id].Scripts.Count);
        if (!admitted)
        {
            return "Native GJMP requires an executed instruction-copy allocation; omitted global pointer zero aliases remain unsupported";
        }
        int target = entry.Script.Start;
        if ((uint)target >= program.Operations.Count || program.Operations[target].Opcode != Jsm.Opcode.LBL)
        {
            return "Native GJMP requires its relocated original WORD pointer to select an executable LBL";
        }
        if (fiber.NativeGlobalJumps.LastOrDefault() is { Entered: false } || HasNativeOperandRecord(fiber.ObjectId) && actors[fiber.ObjectId].NativeOperandMemory?.LiveDepth is not (>= -1 and <= 70))
        {
            return "Native GJMP requires room for its return DWORD and eight LBL result DWORDs in the physical stack";
        }
        return null;
    }

    private void ExecuteNativeGlobalJump(Fiber fiber, JsmOperation operation)
    {
        int target = program!.NativeScriptPointers[operation.Parameter].Script.Start;
        int next = unchecked((ushort)fiber.Position);
        fiber.NativeGlobalJumps.Add(new NativeGlobalJumpFrame(operation.Index, target, next, fiber.Stack.Count, false));
        fiber.Stack.Push(next);
        fiber.StackExpressions.Push("global return instruction");
        fiber.Position = target;
    }

    private bool EnterNativeGlobalResultFrame(Fiber fiber)
    {
        if (fiber.NativeGlobalJumps.LastOrDefault() is not { Entered: false } frame)
        {
            return false;
        }
        if (fiber.Position - 1 != frame.TargetOperation || fiber.Stack.Count != frame.OperandBase + 1)
        {
            throw new NotSupportedException("Native global LBL lost its exact call and return operand");
        }
        foreach (NativeScalarValue value in ReadNativeDoorLiveResults(fiber))
        {
            fiber.Stack.Push(value);
            fiber.StackExpressions.Push("global saved result");
        }
        fiber.Results.Clear();
        if (actors[fiber.ObjectId].NativeCardChallenge)
        {
            fiber.Results[0] = 1;
        }
        fiber.NativeGlobalJumps[^1] = frame with { Entered = true };
        return true;
    }

    private string? UnsupportedNativeGlobalReturn(Fiber fiber, JsmOperation operation)
    {
        if (operation.Parameter != 9 || fiber.NativeGlobalJumps.LastOrDefault() is not { Entered: true } frame)
        {
            return "Native global subroutine requires an entered LBL frame and RET selector9";
        }
        NativeScalarValue[] values = fiber.Stack.Capture();
        if (values.Length != frame.OperandBase + 9 || !values[8].IsExact || unchecked((ushort)values[8].RequireExact()) != frame.ReturnOperation)
        {
            return "Native RET9 requires its eight actual result DWORDs and preserved return WORD";
        }
        return null;
    }

    private bool ExecuteNativeGlobalReturn(Fiber fiber, JsmOperation operation)
    {
        if (operation.Parameter != 9)
        {
            return false;
        }
        NativeScalarValue[] bank = new NativeScalarValue[8];
        for (int index = 0; index < bank.Length; index++)
        {
            bank[index] = fiber.Stack.PopValue();
            fiber.StackExpressions.Pop();
        }
        SetNativeDoorLiveResults(fiber, bank);
        fiber.Position = unchecked((ushort)fiber.Stack.Pop());
        fiber.StackExpressions.Pop();
        actors[fiber.ObjectId].NativeCardChallenge = false;
        fiber.NativeGlobalJumps.RemoveAt(fiber.NativeGlobalJumps.Count - 1);
        return true;
    }

    private void ValidateNativeGlobalJumpCheckpoint(Checkpoint saved)
    {
        foreach (FiberCheckpoint fiber in saved.Fibers)
        {
            NativeGlobalJumpFrame[] frames = fiber.NativeGlobalJumps ?? [];
            if (frames.Length == 0)
            {
                continue;
            }
            if (fiber.Complete || NativeRuntimeProfile is not { IsVerified: true } || frames.Length > 8)
            {
                throw new InvalidDataException("Saved global calls require a live original request and bounded physical frames.");
            }
            int previousBase = -9;
            for (int index = 0; index < frames.Length; index++)
            {
                NativeGlobalJumpFrame frame = frames[index];
                if ((uint)frame.CallOperation >= program!.Operations.Count || program.Operations[frame.CallOperation] is not { Opcode: Jsm.Opcode.GJMP } call || (uint)call.Parameter >= program.NativeScriptPointers.Count || program.NativeScriptPointers[call.Parameter].Script.Start != frame.TargetOperation || frame.ReturnOperation != unchecked((ushort)(frame.CallOperation + 1)) || frame.OperandBase < previousBase + 9 || frame.OperandBase < 0 || !frame.Entered && (index != frames.Length - 1 || fiber.Position != frame.TargetOperation || fiber.Stack.Length != frame.OperandBase + 1) || fiber.Stack.Length < frame.OperandBase + (frame.Entered ? 9 : 1))
                {
                    throw new InvalidDataException("Saved global call lost its original instruction, pointer, frame depth or entry boundary.");
                }
                NativeScalarValue value = fiber.Stack[fiber.Stack.Length - frame.OperandBase - 1];
                if (!value.IsExact || unchecked((ushort)value.RequireExact()) != frame.ReturnOperation)
                {
                    throw new InvalidDataException("Saved global call return operand disagrees with its original WORD instruction pointer.");
                }
                previousBase = frame.OperandBase;
            }
        }
    }
}
