namespace Memoria.FF8.Runtime;

/// <summary>A script scalar, an observed model operand, or the result of a proved side comparison.</summary>
public readonly record struct NativeBattleScriptValue
{
    private enum SideDifference
    {
        None,
        BoundaryMinusActor,
        ActorMinusBoundary
    }

    private readonly int scalar;
    private readonly BattleModelOwner? actor;
    private readonly byte? originalAlias;
    private readonly SideDifference difference;

    private NativeBattleScriptValue(int scalar, BattleModelOwner? actor, byte? originalAlias, SideDifference difference)
    {
        this.scalar = scalar;
        this.actor = actor;
        this.originalAlias = originalAlias;
        this.difference = difference;
    }

    public static NativeBattleScriptValue Scalar(int value) => new(value, null, null, SideDifference.None);

    internal static NativeBattleScriptValue CaptureActor(BattleModelOwner owner, BattleActorModels models, NativeBattleModelBoundary boundary)
    {
        _ = models.Get(owner);
        byte? alias = boundary.TryEncodeExactActor(owner, models, out byte original) ? original : null;
        return new(0, owner, alias, SideDifference.None);
    }

    internal bool IsScalar => actor is null;

    internal short? WordProjection => actor is null ? unchecked((short)scalar) : originalAlias;

    public int RequireScalar()
    {
        if (actor is null)
        {
            return scalar;
        }
        return originalAlias ?? throw new NotSupportedException("The script operation requires an exact original scalar for its model operand.");
    }

    internal static NativeBattleScriptValue Subtract(NativeBattleScriptValue left, NativeBattleScriptValue right)
    {
        const int FirstEnemyOperand = 3;
        if (left.IsScalar && left.scalar == FirstEnemyOperand && right.IsUnaliasedActor)
        {
            return new(0, right.actor, null, SideDifference.BoundaryMinusActor);
        }
        if (right.IsScalar && right.scalar == FirstEnemyOperand && left.IsUnaliasedActor)
        {
            return new(0, left.actor, null, SideDifference.ActorMinusBoundary);
        }
        return Scalar(unchecked(left.RequireScalar() - right.RequireScalar()));
    }

    internal bool TakeBranch(int kind)
    {
        if (difference != SideDifference.None)
        {
            bool enemy = actor!.Side == BattleSide.Enemies;
            return (difference, kind) switch
            {
                (SideDifference.BoundaryMinusActor, 1) or (SideDifference.ActorMinusBoundary, 6) => !enemy,
                (SideDifference.BoundaryMinusActor, 5) or (SideDifference.ActorMinusBoundary, 2) => enemy,
                _ => throw new NotSupportedException("The script comparison requires the original numeric distance from its actor-side boundary.")
            };
        }
        int value = RequireScalar();
        return kind switch
        {
            1 => value > 0,
            2 => value >= 0,
            3 => value == 0,
            4 => value != 0,
            5 => value <= 0,
            _ => value < 0
        };
    }

    private bool IsUnaliasedActor => actor is not null && originalAlias is null && difference == SideDifference.None;
}
