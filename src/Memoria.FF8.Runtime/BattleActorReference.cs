namespace Memoria.FF8.Runtime;

public abstract record BattleActorReference
{
    private readonly int actorIdHash;

    private protected BattleActorReference(string actorId, BattleSide side)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ActorId = actorId;
        Side = side;
        actorIdHash = EqualityComparer<string>.Default.GetHashCode(actorId);
    }

    public string ActorId { get; }
    public BattleSide Side { get; }

    public override int GetHashCode()
    {
        return unchecked((EqualityComparer<Type>.Default.GetHashCode(EqualityContract) * -1521134295 + actorIdHash) * -1521134295 + EqualityComparer<BattleSide>.Default.GetHashCode(Side));
    }
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
