using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusRules(byte BattleSpeed, ImmutableArray<byte> Durations);
public sealed record NativeBattleStatusSnapshot(ushort Saved, uint Battle, short[] Timers)
{
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleStatusMirror Presentation { get; init; } = new(null, null);
    /// <summary>The applied Meltdown reduction. An absent value retains native Vit0 from old saves or other sources.</summary>
    public int? MeltdownDefenseReductionPercent { get; init; }
    public byte[]? Resistances { get; init; }
}
public sealed record NativeBattleStatusAllocation(ushort Saved, uint Battle, ImmutableArray<byte> Resistances);
