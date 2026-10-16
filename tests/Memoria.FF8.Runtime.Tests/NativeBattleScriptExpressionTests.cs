using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptExpressionTests
{
    [Theory]
    [InlineData(0xC6, 13)]
    [InlineData(0xCA, 7)]
    [InlineData(0xCE, 30)]
    [InlineData(0xD2, 3)]
    [InlineData(0xD6, 2)]
    [InlineData(0xDA, 11)]
    [InlineData(0xDE, 9)]
    [InlineData(0xE2, 1)]
    public void ExpressionsUseTheOriginalDwordArithmetic(byte opcode, int expected)
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = 10 };
        int cursor = 1;
        byte ReadByte()
        {
            cursor++;
            return 3;
        }
        NativeBattleScriptExpressions.Execute(opcode, 0, ref cursor, state, ReadByte, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(expected, state.Accumulator);
        Assert.Equal(2, cursor);
    }

    [Theory]
    [InlineData(0xC4, 9, 3)]
    [InlineData(0xC5, 9, 2)]
    [InlineData(0xC6, 265, 2)]
    public void OperandWidthsDistinguishSignedWordSignedByteAndUnsignedByte(byte opcode, int expected, int next)
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = 10 };
        int cursor = 1;
        byte ReadByte()
        {
            cursor++;
            return 255;
        }
        NativeBattleScriptExpressions.Execute(opcode, 0, ref cursor, state, ReadByte, _ => 0, (_, _) => throw new InvalidOperationException());
        Assert.Equal(expected, state.Accumulator);
        Assert.Equal(next, cursor);
    }

    [Theory]
    [InlineData(0xE6, true)]
    [InlineData(0xE7, false)]
    [InlineData(0xE8, true)]
    [InlineData(0xE9, true)]
    [InlineData(0xEA, false)]
    [InlineData(0xEB, true)]
    [InlineData(0xEC, false)]
    [InlineData(0xED, true)]
    [InlineData(0xEE, false)]
    [InlineData(0xEF, true)]
    [InlineData(0xF0, true)]
    [InlineData(0xF1, false)]
    [InlineData(0xF2, true)]
    [InlineData(0xF3, false)]
    public void BranchesAreRelativeToTheirOpcodeAndPreserveTheirSignedWidth(byte opcode, bool taken)
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = 0 };
        int cursor = 101;
        int operand = 0;
        byte ReadByte()
        {
            cursor++;
            return operand++ == 0 ? (byte)0xFE : (byte)0xFF;
        }
        NativeBattleScriptExpressions.Execute(opcode, 100, ref cursor, state, ReadByte, _ => 0, (_, _) => throw new InvalidOperationException());
        Assert.Equal(taken ? 98 : opcode < 0xED ? 102 : 103, cursor);
    }

    [Fact]
    public void TemporaryDwordsAreLocalToOneCallbackAndVariablesAreOwnedByTheCaller()
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = int.MinValue };
        int cursor = 1;
        byte NextTemporary()
        {
            cursor++;
            return 128;
        }
        NativeBattleScriptExpressions.Execute(0xE5, 0, ref cursor, state, NextTemporary, _ => 0, (_, _) => throw new InvalidOperationException());
        state.Accumulator = 0;
        NativeBattleScriptExpressions.Execute(0xC3, 0, ref cursor, state, NextTemporary, _ => 0, (_, _) => throw new InvalidOperationException());
        Assert.Equal(int.MinValue, state.Accumulator);
        Assert.Throws<InvalidDataException>(() => NativeBattleScriptExpressions.Execute(0xC3, 0, ref cursor, new(), NextTemporary, _ => 0, (_, _) => throw new InvalidOperationException()));
    }
}
