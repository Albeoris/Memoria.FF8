namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Body
{
    public static NativeBattleActionMatrix ReadBoneMatrix68A790(System.Collections.Immutable.ImmutableArray<NativeBattleActionMatrix> bones, short sourceBone, System.Collections.Immutable.ImmutableArray<byte> actionBoneAliases = default)
    {
        short bone = NativeBattleActionBoneAliases.Resolve(actionBoneAliases, sourceBone);
        if ((uint)bone >= (uint)bones.Length)
        {
            throw new InvalidDataException("Native Duel matrix selector exceeds its current physical actor skeleton.");
        }
        return bones[bone];
    }

    public static NativeBattleActionMatrix Matrix68D1D0(NativeBattleActionMatrix world, NativeBattleActionMatrix view, NativeBattleVector angles, NativeBattleVector pivot, NativeBattleVector scale)
    {
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles) with { X = pivot.X, Y = pivot.Y, Z = pivot.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, local);
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, pivot);
        return matrix with { X = unchecked(matrix.X - x), Y = unchecked(matrix.Y - y), Z = unchecked(matrix.Z - z) };
    }
}
