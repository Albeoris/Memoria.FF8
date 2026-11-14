using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Source, selection and scalar geometry state for 50D300.</summary>
public sealed record NativeBattleActionGeometry(BattleModelOwner Source, BattleModelTargets SourceTargets, bool SourceHasNonActorSelectors)
{
    public BattleModelTargets? TargetActors { get; init; }
    public BattleModelOwner? Target { get; init; }
    public short? DistanceScale { get; init; }
    public int? UnadjustedDistance { get; init; }
    public int? ContactDistance { get; init; }
    public short? TargetYaw { get; init; }
    public ImmutableArray<NativeBattleActionMatrix>? Matrices { get; init; }
    public NativeBattleActionMatrix? TargetMatrix { get; init; }

    [JsonIgnore]
    public bool IsSelfTarget => !SourceHasNonActorSelectors && SourceTargets.IsSingle(Source);

    public void Validate()
    {
        if (Source is null || SourceTargets is null)
        {
            throw new InvalidDataException("Action geometry requires a model source and its selection.");
        }
        if (TargetActors is { } targets && (targets.IsEmpty || Target is not { } target || !targets.Contains(target)) ||
            TargetActors is null && Target is not null)
        {
            throw new InvalidDataException("Action geometry requires a resolved target within its target set.");
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
