namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestItemSelection(long RequestId, long SubmittedAt, int ActorSlot, int TargetSlot, ushort ItemId);
public sealed record Ff7BattleGuestItemEffect(ushort HitPoints, uint RemoveStatuses = 0, byte ReviveDivisor = 0, byte MaximumHitPointFraction = 0);

/// <summary>Original guest items use a separate origin inventory and queue reservation.</summary>
public interface IFf7BattleGuestItems
{
    string? ItemUnsupportedReason(ushort item) => null;
    string? PrepareItemUnsupportedReason(Ff7BattleGuestPhysicalHit prepared) => null;
    bool HasOriginItems { get; }
    IReadOnlyList<ushort> ItemIds { get; }
    int ItemStock(ushort item);
    bool SupportsItem(ushort item, Ff7BattleGuestAttackTarget target);
    Ff7BattleGuestItemEffect DescribeItem(ushort item);
    void ReserveItem(Ff7BattleGuestItemSelection selection);
    void CancelItem(int target, ushort item);
    void PrepareItem(Ff7BattleGuestPhysicalHit prepared);
}
