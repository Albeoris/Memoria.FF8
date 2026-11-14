namespace Memoria.FF8.Runtime;

public abstract record BattleModelOwner
{
    private protected BattleModelOwner(BattleSide side)
    {
        Side = side;
    }

    public BattleSide Side { get; }
}

public abstract record BattlePartyModelOwner : BattleModelOwner
{
    private protected BattlePartyModelOwner() : base(BattleSide.Allies)
    {
    }
}

public abstract record BattleEnemyModelOwner : BattleModelOwner
{
    private protected BattleEnemyModelOwner() : base(BattleSide.Enemies)
    {
    }
}

public sealed record BattlePartyActorModel : BattlePartyModelOwner
{
    public BattlePartyActorModel(BattlePartyActorReference actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Actor = actor;
    }

    public BattlePartyActorReference Actor { get; }
}

public sealed record BattleEnemyActorModel : BattleEnemyModelOwner
{
    public BattleEnemyActorModel(BattleEnemyActorReference actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Actor = actor;
    }

    public BattleEnemyActorReference Actor { get; }
}

public sealed record BattleForeignPresentationKey
{
    public BattleForeignPresentationKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    public string Key { get; }
}

public sealed record BattleForeignPartyModel : BattlePartyModelOwner
{
    public BattleForeignPartyModel(BattleForeignPresentationKey presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        Presentation = presentation;
    }

    public BattleForeignPresentationKey Presentation { get; }
}

public sealed record BattleForeignEnemyModel : BattleEnemyModelOwner
{
    public BattleForeignEnemyModel(BattleForeignPresentationKey presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        Presentation = presentation;
    }

    public BattleForeignPresentationKey Presentation { get; }
}
