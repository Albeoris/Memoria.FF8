using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHypnotize133State? Hypnotize133
    {
        get => effectState.Hypnotize133;
        init => effectState = effectState with { Hypnotize133 = value };
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBeamLaser134State? BeamLaser134
    {
        get => effectState.BeamLaser134;
        init => effectState = effectState with { BeamLaser134 = value };
    }
}
