using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original kernel inputs for the shared48FE20/4922B0 action consumer, with their exact source record.</summary>
public sealed record BattleNativeAction(byte Command, ushort ActionId, int KernelSection, int KernelIndex)
{
    public byte? DispatchCommand { get; init; }
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
}
