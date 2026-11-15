namespace Memoria.FF8.Runtime;

/// <summary>Per50DB40 callback expression accumulator and stack temporaries; never retained across yields.</summary>
public sealed class NativeBattleScriptExpressionState
{
    public NativeBattleScriptValue? Accumulator { get; set; }
    internal NativeBattleScriptValue?[] Temporaries { get; } = new NativeBattleScriptValue?[128];
}

public static class NativeBattleScriptExpressions
{
    /// <summary>Shared50DB40 C0..F3 dispatch. Cursor initially follows the opcode; branches use its original address.</summary>
    public static void Execute(byte opcode, int origin, ref int cursor, NativeBattleScriptExpressionState state, Func<byte> nextByte, Func<byte, NativeBattleScriptValue> readVariable, Action<byte, NativeBattleScriptValue> writeVariable)
    {
        NativeBattleScriptValue Current() => state.Accumulator ?? throw new InvalidDataException("Native script reads the unresolved initial expression accumulator.");
        if (opcode is >= 0xC0 and < 0xE4)
        {
            NativeBattleScriptValue operand;
            switch (opcode & 3)
            {
                case 0:
                    operand = NativeBattleScriptValue.Scalar(unchecked((short)(nextByte() | nextByte() << 8)));
                    break;
                case 1:
                    operand = NativeBattleScriptValue.Scalar(unchecked((sbyte)nextByte()));
                    break;
                case 2:
                    operand = NativeBattleScriptValue.Scalar(nextByte());
                    break;
                default:
                    byte variable = nextByte();
                    operand = variable >= 128 ? state.Temporaries[variable - 128] ?? throw new InvalidDataException("Native script reads an unwritten temporary DWORD.") : readVariable(variable);
                    break;
            }
            int operation = opcode & 0xFC;
            state.Accumulator = operation switch
            {
                0xC0 => operand,
                0xC8 => NativeBattleScriptValue.Subtract(Current(), operand),
                _ => NativeBattleScriptValue.Scalar(Evaluate(operation, Current().RequireScalar(), operand.RequireScalar()))
            };
            return;
        }
        if (opcode == 0xE4)
        {
            state.Accumulator = NativeBattleScriptValue.Scalar(0);
            return;
        }
        if (opcode == 0xE5)
        {
            byte variable = nextByte();
            if (variable >= 128)
            {
                state.Temporaries[variable - 128] = Current();
            }
            else
            {
                writeVariable(variable, Current());
            }
            return;
        }
        if (opcode is >= 0xE6 and <= 0xF3)
        {
            int kind = (opcode - 0xE6) % 7;
            bool take = kind == 0 || Current().TakeBranch(kind);
            int relative = opcode < 0xED ? unchecked((sbyte)nextByte()) : unchecked((short)(nextByte() | nextByte() << 8));
            if (take)
            {
                cursor = checked(origin + relative);
            }
            return;
        }
        throw new NotSupportedException($"Native expression opcode{opcode:X2} has no original executable contract.");
    }

    private static int Evaluate(int operation, int current, int operand)
    {
        return operation switch
        {
            0xC4 => unchecked(current + operand),
            0xCC => unchecked(current * operand),
            0xD0 => current / operand,
            0xD4 => current & operand,
            0xD8 => current | operand,
            0xDC => current ^ operand,
            0xE0 => current % operand,
            _ => throw new InvalidDataException("Native script has an invalid expression operation.")
        };
    }
}
