using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    /// <summary>56BEF0's signed DWORD column scales, using the existing low-WORD matrix stores.</summary>
    public static NativeBattleActionMatrix Scale56BEF0(NativeBattleActionMatrix matrix, int x, int y, int z)
    {
        matrix.Validate();
        Matrix scaled = Scale(new(matrix.Rotation.ToArray(), matrix.X, matrix.Y, matrix.Z), [x, y, z]);
        return new(scaled.Rotation.ToImmutableArray(), scaled.X, scaled.Y, scaled.Z, matrix.Scale);
    }

    public static NativeBattleActionMatrix Compose56C2F0(NativeBattleActionMatrix left, NativeBattleActionMatrix right)
    {
        left.Validate();
        right.Validate();
        Matrix composed = Compose(new(left.Rotation.ToArray(), left.X, left.Y, left.Z), new(right.Rotation.ToArray(), right.X, right.Y, right.Z));
        return new(composed.Rotation.ToImmutableArray(), composed.X, composed.Y, composed.Z, 4096);
    }
}
