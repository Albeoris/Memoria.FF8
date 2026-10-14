namespace Memoria.FF8.Runtime;

/// <summary>Original signed-DWORD CAL operations. An exact result requires agreement over every represented operand pair.</summary>
public static class NativeScalarCalculation
{
    public static int OperandCount(int kind)
    {
        if ((uint)kind > 17)
        {
            throw new NotSupportedException($"Native CAL operator {kind} is outside its original eighteen-entry table.");
        }
        return kind is 5 or 15 ? 1 : 2;
    }

    public static NativeScalarValue Resolve(int kind, NativeScalarValue? left, NativeScalarValue right)
    {
        int operands = OperandCount(kind);
        right.Validate();
        if (operands == 2)
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }
            left.Validate();
        }
        if (right.ActorInfoOrigin is not null || left?.ActorInfoOrigin is not null)
        {
            throw new NotSupportedException("Native CAL cannot consume an unknown out-of-retained-table actor-info result.");
        }
        int? unanimous = null;
        foreach (int a in operands == 1 ? new[] { 0 } : left!.Values)
        {
            foreach (int b in right.Values)
            {
                int result = kind switch
                {
                    0 => unchecked(a + b),
                    1 => unchecked(a - b),
                    2 => unchecked(a * b),
                    3 => Divide(a, b, remainder: false),
                    4 => Divide(a, b, remainder: true),
                    5 => unchecked(-b),
                    6 => a == b ? 1 : 0,
                    7 => a > b ? 1 : 0,
                    8 => a >= b ? 1 : 0,
                    9 => a < b ? 1 : 0,
                    10 => a <= b ? 1 : 0,
                    11 => a != b ? 1 : 0,
                    12 => a & b,
                    13 => a | b,
                    14 => a ^ b,
                    15 => ~b,
                    16 => a >> b,
                    17 => unchecked(a << b),
                    _ => throw new InvalidOperationException("Validated native CAL table entry is missing.")
                };
                if (unanimous is int known && result != known)
                {
                    throw new NotSupportedException("Native CAL alternatives produce different results; no representative or unproved operand correlation can be selected.");
                }
                unanimous = result;
            }
        }
        return NativeScalarValue.Exact(unanimous!.Value);
    }

    private static int Divide(int dividend, int divisor, bool remainder)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Native CAL IDIV has a zero divisor.");
        }
        if (dividend == int.MinValue && divisor == -1)
        {
            throw new OverflowException("Native CAL IDIV quotient exceeds the signed DWORD range, including when only its remainder is retained.");
        }
        return remainder ? dividend % divisor : dividend / divisor;
    }
}
