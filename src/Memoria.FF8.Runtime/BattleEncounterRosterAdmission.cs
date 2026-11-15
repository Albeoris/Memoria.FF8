using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum BattleEncounterRosterRestriction
{
    NativePartyReplacement,
    ScriptedPartyMembership,
    UnsupportedEnemyLifecycle
}

public sealed record BattleEncounterRosterRestrictionReason(BattleEncounterRosterRestriction Restriction, int? ModelId = null, int? LevelBand = null, int? Callback = null, int? InstructionOffset = null, int? Opcode = null, int? SceneSlot = null);

public sealed record BattleEncounterRosterAdmission(ImmutableArray<BattleEncounterRosterRestrictionReason> Reasons)
{
    public static BattleEncounterRosterAdmission Unrestricted { get; } = new([]);

    public bool UsesPrimaryPartyOnly => Reasons.Any(reason => reason.Restriction is BattleEncounterRosterRestriction.NativePartyReplacement or BattleEncounterRosterRestriction.ScriptedPartyMembership);
    public bool AllowsAdditionalEnemies => !Reasons.Any(reason => reason.Restriction == BattleEncounterRosterRestriction.UnsupportedEnemyLifecycle);

    public static BattleEncounterRosterAdmission ForFormation(int formation, IEnumerable<BattleEncounterRosterRestrictionReason> decodedRestrictions)
    {
        ArgumentNullException.ThrowIfNull(decodedRestrictions);
        // NG-094: The final battle's native party replacement uses the original three saved bindings.
        IEnumerable<BattleEncounterRosterRestrictionReason> reasons = formation == 511
            ? decodedRestrictions.Prepend(new(BattleEncounterRosterRestriction.NativePartyReplacement))
            : decodedRestrictions;
        return new(reasons.Distinct().ToImmutableArray());
    }
}
