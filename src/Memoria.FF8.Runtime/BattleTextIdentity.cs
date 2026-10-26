namespace Memoria.FF8.Runtime;

/// <summary>Presentation provenance only; it never participates in battle execution or duration.</summary>
public sealed record BattleTextIdentity(string File, int Ordinal)
{
    public static BattleTextIdentity Enemy(int modelId, int ordinal) => new($"c0m{modelId:000}_dialogue_battle", ordinal);
    public static BattleTextIdentity Global(int ordinal) => new("kernel_misctexts_text", ordinal);
}

public sealed partial class BattleCombatant
{
    public bool HasNativeDisplayNameOverride => NativeDisplayNameIndex.HasValue;
}
