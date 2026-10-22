using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLookBasisStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattlePackedMatrix32 Matrix);

public static class NativeBattleLookBasis
{
    public static NativeBattleLookBasisStep Execute8DDA50(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattlePackedMatrix32 destination, NativeBattleVector direction, NativeBattleVector up)
    {
        destination.Validate();
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 32);
        NativeBattleGeometryRegisterStep cross = NativeBattleGeometry.LoadDiagonal45DC70(new(memory.GeometryRegisters ?? throw new NotSupportedException("Native look basis requires its geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native look basis requires its data registers.")), up);
        cross = NativeBattleGeometry.LoadSignedIr45DC40(cross, direction);
        cross = NativeBattleGeometry.OuterProduct45E820(cross);
        byte[] vectors = new byte[18];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 2), unchecked((short)cross.Colors.Ir[index]!.Value));
        }
        short[] source = [up.X, up.Y, up.Z, direction.X, direction.Y, direction.Z];
        for (int index = 0; index < source.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(6 + index * 2), source[index]);
        }
        byte[] bytes = destination.Bytes.ToArray();
        bool[] known = destination.Known.ToArray();
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                vectors.AsSpan((column * 3 + row) * 2, 2).CopyTo(bytes.AsSpan((row * 3 + column) * 2, 2));
            }
        }
        Array.Fill(known, true, 0, 18);
        NativeBattleFileStaging released = NativeBattleTemporaryMemory.Release5082D0(allocation.Staging.Write(allocation.Address, vectors), 32);
        return new(memory with { GeometryRegisters = cross.Geometry, ScriptedColorRegisters = cross.Colors }, released, new(bytes.ToImmutableArray(), known.ToImmutableArray()));
    }
}
