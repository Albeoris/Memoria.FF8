using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The graphics header contract is separate from the mechanics action identity and dispatch-packet selection.</summary>
public sealed record BattleNativePresentationInvocation(byte Command, ushort Argument, byte? SourceSequence = null);

/// <summary>Original kernel inputs for the shared48FE20/4922B0 action consumer, with their exact source record.</summary>
public sealed record BattleNativeAction(byte Command, ushort ActionId, int KernelSection, int KernelIndex)
{
    public bool PresentationOnly { get; init; }
    public bool PresentationSecondarySource { get; init; }
    public byte? DispatchCommand { get; init; }
    public BattleNativePresentationInvocation? PresentationInvocation { get; init; }
    public string? Name { get; init; }
    public byte? CrisisLevel { get; init; }
    public int? CharacterId { get; init; }
    public int? SupplementalKernelIndex { get; init; }
    public required ushort MagicId { get; init; }
    public required byte AttackType { get; init; }
    public required byte Power { get; init; }
    public required byte HitCount { get; init; }
    public required byte? TargetFlags { get; init; }
    public required byte AttackFlags { get; init; }
    public required byte DamagePresentation { get; init; }
    public required byte Element { get; init; }
    public required byte? ElementPercent { get; init; }
    public required byte StatusAttack { get; init; }
    public required ushort Statuses0 { get; init; }
    public required uint Statuses1 { get; init; }
    public required byte? Hit { get; init; }
    public required byte? Critical { get; init; }
    public required byte? PowerModifier { get; init; }
    public required byte? LevelModifier { get; init; }
    public required ImmutableArray<byte> RawRecord { get; init; }
    public required ImmutableArray<byte> SupplementalRecord { get; init; }

    internal bool UsesPhysicalDamageFormula => AttackType is 1 or 7 or 9 or 10 or 18 or 34 or 36;
    internal bool UsesMagicDamageFormula => AttackType is 2 or 8 or 11 or 15 or 20 or 22 or 26 or 33;
    internal bool UsesFixedDamageFormula => AttackType is 27 or 28 or 29 or 35;
    internal bool UsesPercentageDamageFormula => AttackType is 7 or 8 or 20;
    internal bool IgnoresImmunity => (Statuses1 & 0x4000000) != 0;
    internal bool HasEarthElement => (Element & 8) != 0;
    internal bool DrainsHitPoints => (Statuses1 & 0x8000) != 0;
    // Drain and the immunity bypass are contact behavior, separate from persistent statuses.
    internal uint AppliedBattleStatuses => Statuses1 & ~0x4008000u;

    public BattleNativePresentationInvocation ReadPresentationInvocation()
    {
        byte command = DispatchCommand ?? Command;
        return PresentationInvocation ?? new(command, command == 11 ? (ushort)65531 : command == 14 ? checked((ushort)(ActionId + 101)) : ActionId);
    }
}
