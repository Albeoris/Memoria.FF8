namespace Memoria.FF8.Runtime;

public abstract record BattleActorReference
{
    private protected BattleActorReference(string actorId, BattleSide side)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ActorId = actorId;
        Side = side;
    }

    public string ActorId { get; }
    public BattleSide Side { get; }
}

public sealed record BattlePartyActorReference : BattleActorReference
{
    public BattlePartyActorReference(string actorId) : base(actorId, BattleSide.Allies)
    {
    }
}

public sealed record BattleEnemyActorReference : BattleActorReference
{
    public BattleEnemyActorReference(string actorId) : base(actorId, BattleSide.Enemies)
    {
    }
}
