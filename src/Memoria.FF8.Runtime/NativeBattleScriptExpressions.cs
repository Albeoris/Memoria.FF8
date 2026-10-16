namespace Memoria.FF8.Runtime;

/// <summary>Per50DB40 callback expression accumulator and stack temporaries; never retained across yields.</summary>
public sealed class NativeBattleScriptExpressionState
{
    public int? Accumulator { get; set; }
    internal int?[] Temporaries { get; } = new int?[128];
}

public static class NativeBattleScriptExpressions
{
    /// <summary>Shared50DB40 C0..F3 dispatch. Cursor initially follows the opcode; branches use its original address.</summary>
    public static void Execute(byte opcode, int origin, ref int cursor, NativeBattleScriptExpressionState state, Func<byte> nextByte, Func<byte, int> readVariable, Action<byte, int> writeVariable)
    {
        int Current() => state.Accumulator ?? throw new InvalidDataException("Native script reads the unresolved initial expression accumulator.");
        if (opcode is >= 0xC0 and < 0xE4)
        {
            int operand;
            switch (opcode & 3)
            {
                case 0:
                    operand = unchecked((short)(nextByte() | nextByte() << 8));
                    break;
                case 1:
                    operand = unchecked((sbyte)nextByte());
                    break;
                case 2:
                    operand = nextByte();
                    break;
                default:
                    byte variable = nextByte();
                    operand = variable >= 128 ? state.Temporaries[variable - 128] ?? throw new InvalidDataException("Native script reads an unwritten temporary DWORD.") : readVariable(variable);
                    break;
            }
            int current = (opcode & 0xFC) == 0xC0 ? 0 : Current();
            state.Accumulator = (opcode & 0xFC) switch
            {
                0xC0 => operand,
                0xC4 => unchecked(current + operand),
                0xC8 => unchecked(current - operand),
                0xCC => unchecked(current * operand),
                0xD0 => current / operand,
                0xD4 => current & operand,
                0xD8 => current | operand,
                0xDC => current ^ operand,
                0xE0 => current % operand,
                _ => throw new InvalidDataException("Native script has an invalid expression operation.")
            };
            return;
        }
        if (opcode == 0xE4)
        {
            state.Accumulator = 0;
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
            int value = kind == 0 ? 0 : Current();
            bool take = kind switch
            {
                0 => true,
                1 => value > 0,
                2 => value >= 0,
                3 => value == 0,
                4 => value != 0,
                5 => value <= 0,
                _ => value < 0
            };
            int relative = opcode < 0xED ? unchecked((sbyte)nextByte()) : unchecked((short)(nextByte() | nextByte() << 8));
            if (take)
            {
                cursor = checked(origin + relative);
            }
            return;
        }
        throw new NotSupportedException($"Native expression opcode{opcode:X2} has no original executable contract.");
    }
}
