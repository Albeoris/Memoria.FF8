using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatefulMatrixRotationStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattlePackedMatrix32 Matrix);

public static class NativeBattleStatefulMatrixRotation
{
    public static NativeBattleStatefulMatrixRotationStep Rotate(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattlePackedMatrix32 matrix, char axis, short angle)
    {
        matrix.Validate();
        if (axis is not ('X' or 'Y' or 'Z'))
        {
            throw new ArgumentOutOfRangeException(nameof(axis));
        }
        for (int index = 0; index < 18; index++)
        {
            if (!matrix.Known[index])
            {
                throw new NotSupportedException("Native matrix rotation requires its actual input rotation bytes.");
            }
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x24);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 0x24);
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Sine((angle + 0x400) & 0xFFF));
        temporary = Word(temporary, 0x20, sine);
        temporary = Word(temporary, 0x22, cosine);
        short negative = unchecked((short)-sine);
        short[] rotation = axis switch
        {
            'X' => [4096, 0, 0, 0, cosine, negative, 0, sine, cosine],
            'Y' => [cosine, 0, sine, 0, 4096, 0, negative, 0, cosine],
            _ => [cosine, negative, 0, sine, cosine, 0, 0, 0, 4096]
        };
        for (int index = 0; index < rotation.Length; index++)
        {
            temporary = Word(temporary, index * 2, rotation[index]);
        }
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown();
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => matrix.Known[index] ? (byte?)matrix.Bytes[index] : null).ToImmutableArray() };
        NativeBattleGeometryRegisterStep registers = new(geometry, memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        byte[] result = matrix.Bytes.ToArray();
        bool[] known = matrix.Known.ToArray();
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)rotation[column]), unchecked((ushort)rotation[column + 3]), unchecked((ushort)rotation[column + 6]));
            registers = NativeBattleGeometry.Transform(registers, translation: false, irInput: true);
            for (int row = 0; row < 3; row++)
            {
                int value = registers.Colors.Ir[row] ?? throw new NotSupportedException("Native matrix rotation requires its saturated IR results.");
                int offset = row * 6 + column * 2;
                BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(offset), unchecked((short)value));
                known[offset] = true;
                known[offset + 1] = true;
            }
        }
        staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 0x24);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x24);
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, staging, new(result.ToImmutableArray(), known.ToImmutableArray()));
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena temporary, int offset, short value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return temporary.Write(offset, bytes);
    }
}
