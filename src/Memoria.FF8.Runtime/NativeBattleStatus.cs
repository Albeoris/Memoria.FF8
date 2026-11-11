using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusRules(byte BattleSpeed, ImmutableArray<byte> Durations);
public sealed record NativeBattleStatusSnapshot(ushort Saved, uint Battle, short[] Timers)
{
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleStatusMirror Presentation { get; init; } = new(null, null);
    private int? meltdownVitalityReductionPercent;
    private int? meltdownSpiritReductionPercent;
    private int? legacyMeltdownReductionPercent;
    /// <summary>The applied Meltdown vitality reduction. An absent value retains native Vit0 from old saves or other sources.</summary>
    public int? MeltdownVitalityReductionPercent
    {
        get => meltdownVitalityReductionPercent ?? legacyMeltdownReductionPercent;
        init => meltdownVitalityReductionPercent = value;
    }
    /// <summary>The applied Meltdown spirit reduction. An absent value retains native Vit0 from old saves or other sources.</summary>
    public int? MeltdownSpiritReductionPercent
    {
        get => meltdownSpiritReductionPercent ?? legacyMeltdownReductionPercent;
        init => meltdownSpiritReductionPercent = value;
    }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MeltdownDefenseReductionPercent
    {
        get => null;
        init => legacyMeltdownReductionPercent = value;
    }
    public byte[]? Resistances { get; init; }
}
public sealed record NativeBattleStatusAllocation(ushort Saved, uint Battle, ImmutableArray<byte> Resistances);
