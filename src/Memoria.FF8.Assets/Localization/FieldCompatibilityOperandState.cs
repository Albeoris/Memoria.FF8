using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Localization;

internal sealed class FieldCompatibilityOperandState
{
    private readonly List<int?> suffix = [];

    public void Reset()
    {
        suffix.Clear();
    }

    public int?[] ReadOperands(int operandCount)
    {
        int?[] operands = new int?[operandCount];
        int available = Math.Min(operandCount, suffix.Count);
        suffix.CopyTo(suffix.Count - available, operands, operandCount - available, available);
        return operands;
    }

    public void Observe(JsmOperation operation)
    {
        Observe(operation.Opcode, operation.Parameter);
    }

    public void Observe(Jsm.Opcode opcode, int parameter)
    {
        switch (opcode)
        {
            case Jsm.Opcode.PSHN_L:
            case Jsm.Opcode.PSHAC:
                suffix.Add(parameter);
                break;
            case Jsm.Opcode.PSHI_L:
            case Jsm.Opcode.PSHM_B:
            case Jsm.Opcode.PSHM_W:
            case Jsm.Opcode.PSHM_L:
            case Jsm.Opcode.PSHSM_B:
            case Jsm.Opcode.PSHSM_W:
            case Jsm.Opcode.PSHSM_L:
                suffix.Add(null);
                break;
            case Jsm.Opcode.CAL:
                ObserveCalculation(parameter);
                break;
            default:
                Reset();
                break;
        }
    }

    private void ObserveCalculation(int kind)
    {
        if ((uint)kind > 17)
        {
            Reset();
            return;
        }
        int? right = Pop();
        int? left = NativeScalarCalculation.OperandCount(kind) == 2 ? Pop() : null;
        int? result = null;
        if (right is int exactRight && (NativeScalarCalculation.OperandCount(kind) == 1 || left is not null))
        {
            try
            {
                result = NativeScalarCalculation.Resolve(kind, left is int exactLeft ? NativeScalarValue.Exact(exactLeft) : null, NativeScalarValue.Exact(exactRight)).RequireExact();
            }
            catch (ArithmeticException)
            {
                // A faulting expression cannot establish a constant operand.
            }
        }
        suffix.Add(result);
    }

    private int? Pop()
    {
        if (suffix.Count == 0)
        {
            return null;
        }
        int? value = suffix[^1];
        suffix.RemoveAt(suffix.Count - 1);
        return value;
    }
}
