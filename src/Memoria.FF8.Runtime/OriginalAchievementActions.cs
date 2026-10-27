namespace Memoria.FF8.Runtime;

public enum OriginalAchievementActionKind
{
    StockDraw,
    DrawPoint,
    CardPlayed,
    RareCardLost,
    CardWin,
    CardsClubMaster,
    FirstSalary,
    FinishGame,
    Ragnarok,
    ChocoLoot,
    MaximumHitPoints
}

/// <summary>A fact captured at an accepted live operation, never reconstructed from inventory or presentation history.</summary>
public readonly record struct OriginalAchievementActionEvent(AchievementEventId EventId, AchievementContextId ContextId, OriginalAchievementActionKind Kind, uint? SquallExperience = null, AchievementOwner? Owner = null, BattleNativeDrawContext? StockDraw = null) : IAchievementEvent
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId.Value) || string.IsNullOrWhiteSpace(ContextId.Value) || !Enum.IsDefined(Kind) || Kind != OriginalAchievementActionKind.FinishGame && SquallExperience is not null || (Kind == OriginalAchievementActionKind.StockDraw) != (StockDraw is not null))
        {
            throw new InvalidDataException("Original achievement action requires a valid operation identity and matching immutable operands.");
        }
        if (StockDraw is { } draw)
        {
            BattleOriginalStockDrawOperation.ValidateResult(draw);
        }
    }
}

public sealed record OriginalAchievementDrawCursor(string OccurrenceId, int Operations);

/// <summary>One executed player Stock operation, including its native quantity and actual stock delta.</summary>
public sealed record BattleOriginalStockDrawOperation(int Ordinal, string ActorId, string SourceId, BattleNativeDrawContext Result)
{
    public void Validate()
    {
        if (Ordinal <= 0 || string.IsNullOrWhiteSpace(ActorId) || string.IsNullOrWhiteSpace(SourceId))
        {
            throw new InvalidDataException("Stock Draw operation requires its exact ordinal and combatant owners.");
        }
        ValidateResult(Result);
    }

    internal static void ValidateResult(BattleNativeDrawContext? result)
    {
        if (result is null || result.Selection is 0 or >= 64 || result.Quantity > 100 || result.Added > result.Quantity || result.Failed != (result.Quantity == 0))
        {
            throw new InvalidDataException("Stock Draw operation requires its exact spell, quantity and inventory receipt.");
        }
    }
}
