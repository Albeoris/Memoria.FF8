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
    ConfigureEncounters,
    InstantDefeat,
    SetGunbladeMode,
    GunbladeTrigger,
    ShotTrigger,
    GuardianBoost
}

/// <summary>Live combat controls and settings for the current game.</summary>
public sealed record BattleInput(BattleInputKind Kind, int Frames = 1, string? ActorId = null, string? TargetId = null, BattleAtbMode Mode = BattleAtbMode.Normal, bool FullCombat = false, BattleAbility Ability = BattleAbility.Attack, int ActionId = 0, bool DrawCast = false, bool? RandomEncounters = null, byte? NativeSeed = null, BattleGunbladeMode GunbladeMode = BattleGunbladeMode.Manual)
{
    public string? DrawCastTargetId { get; init; }
    public System.Collections.Immutable.ImmutableArray<string> MagicTargets { get; init; } = [];
    public void Validate()
    {
        if (DrawCastTargetId is not null && (string.IsNullOrWhiteSpace(DrawCastTargetId) || DrawCastTargetId.Length > 128 || Kind != BattleInputKind.Action || Ability != BattleAbility.Draw || !DrawCast))
        {
            throw new InvalidDataException("Draw Cast recipient belongs only to an explicit Draw Cast input.");
        }
        if (MagicTargets.IsDefault || MagicTargets.Length > 3 || !MagicTargets.IsEmpty && (Kind != BattleInputKind.Action || Ability != BattleAbility.Magic || MagicTargets[0] != TargetId || MagicTargets.Any(target => string.IsNullOrWhiteSpace(target) || target.Length > 128)))
        {
            throw new InvalidDataException("Repeated Magic input requires up to three target identities for one spell command.");
        }
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Mode) || !Enum.IsDefined(Ability) || !Enum.IsDefined(GunbladeMode) || ActionId < 0 || Frames is < 1 or > 30000 || ActorId?.Length > 128 || TargetId?.Length > 128 || NativeSeed is not null && Kind != BattleInputKind.StartEncounter || Kind is BattleInputKind.Attack or BattleInputKind.Action && (string.IsNullOrWhiteSpace(ActorId) || string.IsNullOrWhiteSpace(TargetId)))
        {
            throw new InvalidDataException("Invalid battle input.");
        }
        if (Kind == BattleInputKind.ShotTrigger && string.IsNullOrWhiteSpace(ActorId))
        {
            throw new InvalidDataException("Shot trigger requires its active process owner.");
        }
    }
}
