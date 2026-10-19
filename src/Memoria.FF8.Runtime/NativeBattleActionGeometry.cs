using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Owned50D300 actor/selected-recipient and scalar geometry globals; unresolved packet masks retain unknown fields.</summary>
public sealed record NativeBattleActionGeometry(byte ActorIndex, ushort SourceTargetMask)
{
    public ushort? PhysicalTargetMask { get; init; }
    public byte? TargetIndex { get; init; }
    public short? DistanceScale { get; init; }
    public int? UnadjustedDistance { get; init; }
    public int? ContactDistance { get; init; }
    public short? TargetYaw { get; init; }
    public ImmutableArray<NativeBattleActionMatrix>? Matrices { get; init; }
    public NativeBattleActionMatrix? TargetMatrix { get; init; }

    public void Validate()
    {
        if (ActorIndex >= 7 || TargetIndex >= 7 || PhysicalTargetMask is ushort mask && (mask & ~127) != 0)
        {
            throw new InvalidDataException("Native action geometry requires physical graphics indices and a seven-bit target mask.");
        }
        if (Matrices is { } matrices)
        {
            if (matrices.IsDefault || matrices.Length != 5)
            {
                throw new InvalidDataException("Native50D300 requires all five captured source/recipient matrices.");
            }
            foreach (NativeBattleActionMatrix matrix in matrices)
            {
                if (matrix is null)
                {
                    throw new InvalidDataException("Native action matrix bank contains an absent physical matrix.");
                }
                matrix.Validate();
            }
        }
        TargetMatrix?.Validate();
    }
}

public static partial class NativeBattleActionGeometryRules
{
    /// <summary>50D6B0/50CBA0's signed WORD distance scale, including vector reduction and PC rounded square before truncating sqrt.</summary>
    public static short DistanceScale(NativeBattleVector first, NativeBattleVector second)
    {
        var (x, y, z, factor) = ReducedDifference(first, second);
        if ((x | y | z) == 0)
        {
            return 4096;
        }
        float squared = (float)((double)z * z + (double)y * y + (double)x * x);
        int roundedSquare = unchecked((int)((uint)BitConverter.DoubleToInt64Bits(squared + 4503601774854144d) + 0x80000000u));
        int length = unchecked(factor * (int)Math.Sqrt(roundedSquare));
        return unchecked((short)((length << 12) / 5000));
    }

    public static int ContactDistance(short distanceScale, short actorScale38, short recipientScale38) => unchecked((5000 * distanceScale >> 12) - (2000 * (actorScale38 + recipientScale38) >> 12));
}
