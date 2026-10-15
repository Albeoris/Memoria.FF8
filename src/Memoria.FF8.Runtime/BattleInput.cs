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
    InstantVictory,
    Action,
    ConfigureEncounters
}

/// <summary>Ordinary battle inputs; replay recomputes outcomes rather than restoring old battle snapshots.</summary>
public sealed record BattleInput(BattleInputKind Kind, int Frames = 1, string? ActorId = null, string? TargetId = null, BattleAtbMode Mode = BattleAtbMode.Normal, bool FullCombat = false, BattleAbility Ability = BattleAbility.Attack, int ActionId = 0, bool DrawCast = false, bool? RandomEncounters = null, byte? NativeSeed = null)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Mode) || !Enum.IsDefined(Ability) || ActionId < 0 || Frames is < 1 or > 30000 || ActorId?.Length > 128 || TargetId?.Length > 128 || Kind is BattleInputKind.Attack or BattleInputKind.Action && (string.IsNullOrWhiteSpace(ActorId) || string.IsNullOrWhiteSpace(TargetId)))
        {
            throw new InvalidDataException("Invalid battle input.");
        }
    }
}
