using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleComposedAnchorTransform
{
    public static (NativeBattleEffectMemory Memory, NativeBattleVector Position) Transform(NativeBattleEffectMemory memory, NativeBattleActionMatrix matrix, ImmutableArray<byte> mouthAnchor)
    {
        RequireAnchor(mouthAnchor);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Composed anchor requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Composed anchor requires its current arithmetic registers."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan().Slice(4)));
        short w = BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan().Slice(6));
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
        registers = NativeBattleGeometry.Transform(registers);
        NativeBattleVector output = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        return (memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, output);
    }

    private static void RequireAnchor(ImmutableArray<byte> anchor)
    {
        if (anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Composed anchor requires its original packed eight-byte anchor.");
        }
    }
}
