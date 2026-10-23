using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A skip request belongs to one admitted summon and retains its finale boundary across callbacks and saves.</summary>
public sealed record NativeBattleGuardianAnimationSkip(ushort Effect, bool FinaleReached = false);

public sealed partial record NativeBattleGraphicsState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGuardianAnimationSkip? GuardianAnimationSkip { get; init; }
}
