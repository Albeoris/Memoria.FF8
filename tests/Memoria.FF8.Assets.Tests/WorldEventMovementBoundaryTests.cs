using System.Buffers.Binary;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldEventMovementBoundaryTests
{
    [Theory]
    [InlineData(10, 10, 90, 90, true)]
    [InlineData(99, 90, 100, 90, false)]
    [InlineData(90, 100, 90, 101, false)]
    [InlineData(100, 101, 101, 102, true)]
    [InlineData(2047, 90, 2048, 90, false)]
    [InlineData(-8093, -8102, -8092, -8102, false)]
    public void SweptMovementPreservesTheNativeStrictCoordinatePredicates(int beforeX, int beforeZ, int afterX, int afterZ, bool constant)
    {
        (ushort Opcode, ushort Operand)[] instructions = [(0xFF01, 0), (0xFF0F, 100), (0xFF12, 100), (0xFF04, 0), (0xFF16, 0)];
        byte[] program = new byte[8 + instructions.Length * 4];
        BinaryPrimitives.WriteUInt32LittleEndian(program, 8);
        for (int index = 0; index < instructions.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(8 + index * 4), instructions[index].Opcode);
            BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(10 + index * 4), instructions[index].Operand);
        }
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(program, new byte[24]).ReadWorldEvents(program);
        Assert.Equal(constant, reader.HasConstantPositionPredicates(beforeX, beforeZ, afterX, afterZ));
    }
}
