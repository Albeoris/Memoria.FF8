namespace Memoria.FF8.Runtime;

public enum BattleInputKind
{
    ConfigurePlaythrough,
    StartEncounter,
    Tick,
    Attack,
    AdvanceTurn,
    SetMode,
    Return,
    InstantVictory
}

/// <summary>Ordinary battle inputs; replay recomputes outcomes rather than restoring old battle snapshots.</summary>
public sealed record BattleInput(BattleInputKind Kind, int Frames = 1, string? ActorId = null, string? TargetId = null, BattleAtbMode Mode = BattleAtbMode.Normal, bool FullCombat = false)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Mode) || Frames is < 1 or > 30000 || ActorId?.Length > 128 || TargetId?.Length > 128 || Kind == BattleInputKind.Attack && (string.IsNullOrWhiteSpace(ActorId) || string.IsNullOrWhiteSpace(TargetId)))
        {
            throw new InvalidDataException("Invalid battle input.");
        }
    }
}
