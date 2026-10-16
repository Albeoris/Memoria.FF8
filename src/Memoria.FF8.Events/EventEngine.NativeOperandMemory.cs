using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>52BC00/530710 physical eighty-DWORD stack and BYTE380..387. Nulls preserve unknown older state.</summary>
public sealed record NativeActorOperandMemory([property: JsonRequired] int Version, [property: JsonRequired] NativeScalarValue?[] Words, [property: JsonRequired] byte?[] SavedDepths, [property: JsonRequired] int? LiveDepth)
{
    public static NativeActorOperandMemory Unknown() => new(1, new NativeScalarValue?[80], new byte?[8], null);

    public static NativeActorOperandMemory Allocated()
    {
        byte?[] depths = Enumerable.Repeat<byte?>(0, 8).ToArray();
        depths[0] = 255;
        return new(1, Enumerable.Range(0, 80).Select(_ => (NativeScalarValue?)NativeScalarValue.Exact(0)).ToArray(), depths, -1);
    }

    public void Validate()
    {
        if (Version != 1 || Words is not { Length: 80 } || SavedDepths is not { Length: 8 } || LiveDepth is < -1 or > 79 || SavedDepths.Any(depth => depth is > 79 and not 255))
        {
            throw new InvalidDataException("Physical actor operands require their eighty DWORDs, eight byte depths and bounded current depth.");
        }
        foreach (NativeScalarValue? word in Words)
        {
            word?.Validate();
        }
    }

    public NativeActorOperandMemory Push(NativeScalarValue value)
    {
        Validate();
        if (LiveDepth is not int depth)
        {
            return this;
        }
        if (depth >= 79)
        {
            throw new NotSupportedException("Native physical operand push exceeds its eighty-DWORD record.");
        }
        Words[depth + 1] = value;
        return this with { LiveDepth = depth + 1 };
    }

    public NativeActorOperandMemory Pop()
    {
        Validate();
        if (LiveDepth is not int depth)
        {
            return this;
        }
        if (depth < 0)
        {
            throw new NotSupportedException("Native physical operand pop requires a retained word.");
        }
        return this with { LiveDepth = depth - 1 };
    }
}

public sealed partial class EventEngine
{
    private bool HasNativeOperandRecord(int objectId) => NativeRuntimeProfile is { IsVerified: true } && program!.NativeObjects.Take(program.NativeActorCount).Contains(objectId);

    private void BindNativeOperandMemory(Fiber fiber)
    {
        if (!HasNativeOperandRecord(fiber.ObjectId))
        {
            return;
        }
        fiber.Stack.Pushed = value => PushNativePhysicalOperand(fiber.ObjectId, value);
        fiber.Stack.Popped = () => PopNativePhysicalOperand(fiber.ObjectId);
    }

    private void PushNativePhysicalOperand(int objectId, NativeScalarValue value)
    {
        ActorState actor = actors[objectId];
        actor.NativeOperandMemory = (actor.NativeOperandMemory ?? NativeActorOperandMemory.Unknown()).Push(value);
    }

    private void PopNativePhysicalOperand(int objectId)
    {
        ActorState actor = actors[objectId];
        actor.NativeOperandMemory = (actor.NativeOperandMemory ?? NativeActorOperandMemory.Unknown()).Pop();
    }

    private void SaveNativePhysicalDepth(Fiber current)
    {
        NativeActorOperandMemory? memory = actors[current.ObjectId].NativeOperandMemory;
        if (memory?.LiveDepth is int depth)
        {
            memory.SavedDepths[NativeActorRequestSlot(current)] = unchecked((byte)depth);
        }
    }

    private NativeScalarValue[] ReadNativeExplicitReturnBank(Fiber target)
    {
        NativeActorOperandMemory memory = actors[target.ObjectId].NativeOperandMemory ?? throw new NotSupportedException("Native explicit RET to an unentered slot requires its retained physical operand memory.");
        memory.Validate();
        byte saved = memory.SavedDepths[NativeActorRequestSlot(target)] ?? throw new NotSupportedException("Native explicit RET to an unentered slot requires its actual saved BYTE380 depth; no initial depth is inferred.");
        int depth = unchecked((sbyte)saved);
        if (depth is < -1 or > 71 || memory.LiveDepth is null)
        {
            throw new NotSupportedException("Native explicit RET requires a represented current depth and bounded second result bank.");
        }
        return Enumerable.Range(depth + 1, 8).Reverse().Select(index => memory.Words[index] ?? throw new NotSupportedException($"Native explicit RET requires retained physical DWORD{index}, including popped operand writes.")).ToArray();
    }

    private NativeScalarValue[] ReadNativeReturnBank(Fiber fiber)
    {
        if (!HasNativeOperandRecord(fiber.ObjectId))
        {
            if (fiber.Stack.Count != 0 || fiber.NativeDoorSavedResults is not { Length: 8 } saved)
            {
                throw new NotSupportedException("Native non-actor RET requires its represented balanced LBL result frame.");
            }
            return saved.Reverse().ToArray();
        }
        NativeActorOperandMemory memory = actors[fiber.ObjectId].NativeOperandMemory ?? throw new NotSupportedException("Native RET requires its physical operand record.");
        memory.Validate();
        if (memory.LiveDepth is not int depth || depth < 7)
        {
            throw new NotSupportedException("Native RET requires eight actual retained physical DWORDs at the current depth.");
        }
        return Enumerable.Range(depth - 7, 8).Reverse().Select(index => memory.Words[index] ?? throw new NotSupportedException($"Native RET requires known physical DWORD{index}.")).ToArray();
    }

    private NativeScalarValue[] ReadNativeResumedOperands(Fiber target, int resumedDepth)
    {
        if (!target.NativeResultFrameEntered && !target.NativeDoorDefaultFrame)
        {
            // A queued slot has no executed LBL frame or local operand view yet.
            // Its first LBL will start at the actual shared physical depth.
            return [];
        }
        NativeActorOperandMemory memory = actors[target.ObjectId].NativeOperandMemory ?? throw new NotSupportedException("Native RET requires its lower slot physical operand record.");
        byte saved = memory.SavedDepths[NativeActorRequestSlot(target)] ?? throw new NotSupportedException("Native RET requires its lower slot saved operand depth.");
        int operandBase = unchecked((sbyte)saved) - target.Stack.Count;
        if (operandBase < -1 || resumedDepth < operandBase || resumedDepth > 79)
        {
            throw new NotSupportedException("Native RET consumed the lower frame or exceeded its represented operand domain.");
        }
        return Enumerable.Range(operandBase + 1, resumedDepth - operandBase).Select(index => memory.Words[index] ?? throw new NotSupportedException($"Native RET requires lower operand DWORD{index}.")).ToArray();
    }

    private void RestoreNativeResumedOperandView(Fiber target, int resumedDepth)
    {
        NativeScalarValue[] operands = ReadNativeResumedOperands(target, resumedDepth);
        target.Stack.ReplaceRetainedView(operands);
        if (target.StackExpressions.Count > operands.Length)
        {
            while (target.StackExpressions.Count > operands.Length)
            {
                target.StackExpressions.Pop();
            }
        }
        while (target.StackExpressions.Count < operands.Length)
        {
            target.StackExpressions.Push("retained native physical operand");
        }
    }

    private NativeScalarValue[] PopNativeReturnBank(Fiber fiber)
    {
        NativeScalarValue[] bank = ReadNativeReturnBank(fiber);
        for (int index = 0; HasNativeOperandRecord(fiber.ObjectId) && index < bank.Length; index++)
        {
            PopNativePhysicalOperand(fiber.ObjectId);
        }
        return bank;
    }
}
