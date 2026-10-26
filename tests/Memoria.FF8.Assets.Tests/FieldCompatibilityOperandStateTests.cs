using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldCompatibilityOperandStateTests
{
    [Fact]
    public void UnknownChoiceOperandPreservesIndependentTextId()
    {
        FieldCompatibilityContext context = MessageContext([Push(0), Push(7), Push(2), Push(3), Push(2), Operation(Jsm.Opcode.PSHI_L), Push(100), Push(10), Operation(Jsm.Opcode.AASK)]);
        Assert.Equal(new int?[] { 0, 7, 2, 3, 2, null, 100, 10 }, context.Operands);
        Assert.Equal(7, context.MessageId);
        Assert.True(context.Dynamic);
        Assert.Empty(context.CandidateOrdinals);
    }

    [Fact]
    public void UnknownCoordinatePreservesIndependentTextId()
    {
        FieldCompatibilityContext context = MessageContext([Push(0), Push(7), Operation(Jsm.Opcode.PSHSM_W, 10), Push(20), Operation(Jsm.Opcode.AMESW)]);
        Assert.Equal(new int?[] { 0, 7, null, 20 }, context.Operands);
        Assert.Equal(7, context.MessageId);
    }

    [Fact]
    public void ActorConstantPushRetainsItsEncodedValue()
    {
        FieldCompatibilityContext context = MessageContext([Operation(Jsm.Opcode.PSHAC, 3), Push(7), Operation(Jsm.Opcode.MES)]);
        Assert.Equal(new int?[] { 3, 7 }, context.Operands);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 5)]
    [InlineData(2, 14)]
    [InlineData(3, 3)]
    [InlineData(4, 1)]
    [InlineData(6, 0)]
    [InlineData(7, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 0)]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    [InlineData(12, 2)]
    [InlineData(13, 7)]
    [InlineData(14, 5)]
    [InlineData(16, 1)]
    [InlineData(17, 28)]
    public void ConstantBinaryCalculationsUseNativeOperandOrder(int kind, int expected)
    {
        FieldCompatibilityContext context = MessageContext([Push(0), Push(7), Push(2), Operation(Jsm.Opcode.CAL, kind), Operation(Jsm.Opcode.MES)]);
        Assert.Equal(expected, context.MessageId);
    }

    [Theory]
    [InlineData(5, -7)]
    [InlineData(15, -8)]
    public void ConstantUnaryCalculationsConsumeOnlyOneOperand(int kind, int expected)
    {
        FieldCompatibilityContext context = MessageContext([Push(3), Push(7), Operation(Jsm.Opcode.CAL, kind), Operation(Jsm.Opcode.MES)]);
        Assert.Equal(new int?[] { 3, expected }, context.Operands);
    }

    [Fact]
    public void UnknownCalculationDoesNotInferAnIdentity()
    {
        FieldCompatibilityContext context = MessageContext([Push(0), Operation(Jsm.Opcode.PSHM_W, 10), Push(0), Operation(Jsm.Opcode.CAL, 2), Operation(Jsm.Opcode.MES)]);
        Assert.Null(context.MessageId);
    }

    [Fact]
    public void StoreResetsPreviouslyObservedOperands()
    {
        FieldCompatibilityContext context = MessageContext([Push(3), Push(7), Operation(Jsm.Opcode.POPI_L), Push(8), Operation(Jsm.Opcode.MES)]);
        Assert.Equal(new int?[] { null, 8 }, context.Operands);
    }

    [Fact]
    public void FaultingCalculationDoesNotProduceTextId()
    {
        FieldCompatibilityContext context = MessageContext([Push(0), Push(7), Push(0), Operation(Jsm.Opcode.CAL, 3), Operation(Jsm.Opcode.MES)]);
        Assert.Null(context.MessageId);
    }

    private static FieldCompatibilityContext MessageContext(int[] body)
    {
        int[] operations = new[] { Operation(Jsm.Opcode.LBL) }.Concat(body).Append(Operation(Jsm.Opcode.RET)).ToArray();
        byte[] script = new byte[14 + operations.Length * 4];
        script[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(6), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(14 + index * 4), operations[index]);
        }
        FieldCompatibilityInput input = new("fixture", "en", "synthetic", script, []);
        return FieldCompatibilityAudit.Inspect(input).Contexts.Single(context => context.IsMessage);
    }

    private static int Push(int value) => Operation(Jsm.Opcode.PSHN_L, value);

    private static int Operation(Jsm.Opcode opcode, int parameter = 0) => opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.CAL or Jsm.Opcode.LBL ? ((int)opcode << 24) | (parameter & 0xFFFFFF) : (int)opcode;
}
