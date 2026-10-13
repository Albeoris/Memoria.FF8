using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool IsKnownNativeByteAlias(int address, int value)
    {
        return address == 86 && Scene.PreviousWorldEntityByte86 is byte entity && value == entity;
    }

    private static int NativeMemoryWidth(Jsm.Opcode opcode)
    {
        return opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
    }

    private string? UnsupportedNativeScalarAccess(Fiber fiber, JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        bool writing = operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
        if (width > 0 && services.NativeVariableValues is { Count: > 0 } memory && memory.Any(pair => pair.Key < (long)operation.Parameter + width && pair.Key + pair.Value.Width > operation.Parameter && (pair.Key != operation.Parameter || pair.Value.Width != width)))
        {
            return "Native uncertain memory requires a matching address and width; overlapping aliases remain unsupported";
        }
        if (writing && fiber.Stack.Count > 0 && !fiber.Stack.PeekValue().IsExact)
        {
            long last = (long)operation.Parameter + width - 1;
            if (!services.CanStoreNativeVariableValues || operation.Parameter < 0 || last > ushort.MaxValue - 3 || operation.Parameter <= 86 && last >= 84 || operation.Parameter <= 107 && last >= 101 || operation.Parameter <= 217 && last >= 179 || operation.Parameter <= 242 && last >= 240)
            {
                return "Uncertain native store needs a typed memory service and cannot replace separately owned loader/control bytes";
            }
            if (variables.Keys.Any(address => address != operation.Parameter && address >= operation.Parameter && address <= last))
            {
                return "Uncertain native store overlaps a legacy untyped memory alias";
            }
        }
        if (!fiber.Stack.HasUncertainty || operation.Opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.POPI_L or Jsm.Opcode.LBL or Jsm.Opcode.NOP || writing)
        {
            return null;
        }
        return "This command requires an exact native integer; uncertain operands are preserved without choosing a representative";
    }

    private bool TryReadNativeScalar(JsmOperation operation, out NativeScalarValue value)
    {
        if (services.NativeVariableValues is { } memory && memory.TryGetValue(operation.Parameter, out NativeMemoryValue? entry))
        {
            value = entry.Value.ToWidth(NativeMemoryWidth(operation.Opcode), operation.Opcode is Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHSM_W);
            return true;
        }
        value = NativeScalarValue.Exact(0);
        return false;
    }

    private NativeScalarValue PopNativeScalar(Fiber fiber)
    {
        if (fiber.Stack.Count == 0)
        {
            throw Fault(fiber, "Operand stack underflow: need one native scalar.");
        }
        NativeScalarValue value = fiber.Stack.PopValue();
        fiber.LastExpressions = [fiber.StackExpressions.Pop()];
        return value;
    }

    private NativeScalarValue StoreNativeScalar(Fiber fiber, JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        NativeScalarValue raw = fiber.Stack.PeekValue();
        NativeScalarValue value = raw.ToWidth(width);
        Dictionary<int, NativeMemoryValue> next = services.NativeVariableValues is { } memory ? new(memory) : [];
        next.Remove(operation.Parameter);
        if (!value.IsExact)
        {
            next.Add(operation.Parameter, new NativeMemoryValue(width, value));
        }
        bool hadKnown = variables.Remove(operation.Parameter, out int previous);
        try
        {
            services.SetNativeVariableValues(next);
        }
        catch
        {
            if (hadKnown)
            {
                variables[operation.Parameter] = previous;
            }
            throw;
        }
        if (value.IsExact)
        {
            variables[operation.Parameter] = value.RequireExact();
        }
        PopNativeScalar(fiber);
        return raw;
    }

    private void ValidateNativeScalarSource(NativeScalarValue value, int fieldId, string sourceHash, long frame, NativeFieldInitializationProof? initialization, NativeExamTimerState? timer)
    {
        value.Validate();
        if (value.Origin is not { } origin || origin.FieldId != fieldId)
        {
            return;
        }
        EventScript? script = (uint)origin.ObjectId < program!.Objects.Count ? program.Objects[origin.ObjectId].Scripts.FirstOrDefault(script => script.Id == origin.ScriptId) : null;
        // Existing timers advance before this load's first VM turn. A read at or
        // before the captured allocation clock belongs to an earlier load, whose
        // local frame must not be compared with the reset current field frame.
        bool earlierLoad = initialization?.TimerClockCallsAtAllocation is long allocationClock && origin.Timer.ClockCalls <= allocationClock;
        if (origin.ProgramHash != sourceHash || !earlierLoad && origin.Frame > frame || timer is not null && origin.Timer.ClockCalls > timer.ClockCalls || script is null || origin.Operation < script.Start || origin.Operation >= script.End || program.Operations[origin.Operation].Opcode != Jsm.Opcode.GETTIMER)
        {
            throw new InvalidDataException("Native scalar source does not identify its original executed GETTIMER operation.");
        }
    }
}
