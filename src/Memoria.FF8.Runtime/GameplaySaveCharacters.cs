using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Character progression without installed stat tables, model data or native allocation identities.</summary>
public sealed record GameplaySaveCharacters
{
    [JsonRequired]
    public required NativeCharacterVital[] Records { get; init; }
    [JsonRequired]
    public required byte[] CombatParty { get; init; }
    [JsonRequired]
    public required bool DreamEnabled { get; init; }
    [JsonRequired]
    public required GameplaySaveGuardianForce[] GuardianForces { get; init; }
    [JsonRequired]
    public required byte[] DreamWeapons { get; init; }
    [JsonRequired]
    public required byte[] JunctionPartyBackup { get; init; }
    [JsonRequired]
    public required GameplaySaveMagic Magic { get; init; }

    public static GameplaySaveCharacters Capture(NativeCharacterVitals source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        if (source.GuardianForceProfile is not null || source.GuardianForceRecoveryProfile is not null || !source.GuardianForceRecoveryProgression.IsEmpty || !source.GuardianForceRecoveryTemplates.IsEmpty || source.GuardianForceOpaqueBaseline || source.GuardianForces.Any(gf => gf.LastProjection is not null))
        {
            throw new NotSupportedException("Gameplay character projection requires a resolved GF profile without pending compatibility recovery or historical projection tables.");
        }
        GameplaySaveCharacters result = new()
        {
            Records = source.Records,
            CombatParty = source.CombatParty,
            DreamEnabled = source.DreamEnabled,
            GuardianForces = source.GuardianForces.Select(GameplaySaveGuardianForce.Capture).ToArray(),
            DreamWeapons = source.DreamWeapons,
            JunctionPartyBackup = source.JunctionPartyBackup,
            Magic = GameplaySaveMagic.Capture(source.MagicSystem)
        };
        result.Validate();
        return GameplaySaveCodec.Detach(result);
    }

    /// <summary>Combines saved progression with current installed definitions; it does not initialize a scene or execute scripts.</summary>
    public NativeCharacterVitals Restore(NativeCharacterVitals installed)
    {
        ArgumentNullException.ThrowIfNull(installed);
        installed.Validate();
        Validate();
        if (installed.GuardianForceProfile is not null || installed.GuardianForceRecoveryProfile is not null || !installed.GuardianForceRecoveryProgression.IsEmpty || !installed.GuardianForceRecoveryTemplates.IsEmpty || installed.GuardianForceOpaqueBaseline)
        {
            throw new NotSupportedException("Gameplay character restoration requires a resolved installed GF profile.");
        }
        GameplaySaveCharacters detached = GameplaySaveCodec.Detach(this);
        NativeGuardianForceVital[] guardianForces = detached.GuardianForces.Select(gf => gf.Restore()).ToArray();
        for (int index = 0; index < guardianForces.Length; index++)
        {
            guardianForces[index] = guardianForces[index] with { MaximumHitPoints = installed.Definitions.GuardianForceMaximum(index, guardianForces[index]) };
        }
        NativeCharacterVitals result = installed with
        {
            Records = detached.Records,
            CombatParty = detached.CombatParty,
            DreamEnabled = detached.DreamEnabled,
            GuardianForces = guardianForces,
            DreamWeapons = detached.DreamWeapons,
            JunctionPartyBackup = detached.JunctionPartyBackup,
            MagicSystem = detached.Magic.Restore(),
            GuardianForceRecoveryProgression = [],
            GuardianForceRecoveryTemplates = [],
            GuardianForceCompatibilityDiagnostics = []
        };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Records is not { Length: 8 } || CombatParty is not { Length: 3 } || CombatParty.Any(slot => slot != 255 && slot >= 8) || GuardianForces is not { Length: 16 } || DreamWeapons is not { Length: 3 } || JunctionPartyBackup is not { Length: 6 } || Magic is null)
        {
            throw new InvalidDataException("Gameplay characters require eight records, three party slots, sixteen GFs and complete dream/junction state.");
        }
        foreach (NativeCharacterVital record in Records)
        {
            if (record is null || record.HitPoints is null || record.HitPoints.Minimum != record.HitPoints.Maximum || record.CharacterId > 10 || record.Junction is null || record.StatBonuses is not { Length: 6 } || record.MagicInventory is not { Length: 32 } || record.MagicInventory.Any(stock => stock is null) || record.GuardianForceCompatibility is { Length: not 16 })
            {
                throw new InvalidDataException("Gameplay character records require exact HP, junctions, inventory and bounded saved arrays.");
            }
            record.HitPoints.Validate();
            record.Junction.Validate();
        }
        foreach (GameplaySaveGuardianForce gf in GuardianForces)
        {
            if (gf is null)
            {
                throw new InvalidDataException("Gameplay GF progression is missing.");
            }
            gf.Validate();
        }
        Magic.Validate();
    }
}

public sealed record GameplaySaveGuardianForce([property: JsonRequired] uint Experience, [property: JsonRequired] byte Flags, [property: JsonRequired] ushort HitPoints, [property: JsonRequired] NativeGuardianForceLearnedBank LearnedBank, [property: JsonRequired] ImmutableArray<byte> AbilityProgress, [property: JsonRequired] byte Learning, [property: JsonRequired] uint ForgottenAbilities)
{
    public bool MoveFindDisabled { get; init; }
    public NativeGuardianForceOpaqueBytes? OpaqueSavedValues { get; init; }
    public GuardianForceProgression? Progression { get; init; }
    public ushort? KillCount { get; init; }
    public ushort? DeathCount { get; init; }

    internal static GameplaySaveGuardianForce Capture(NativeGuardianForceVital source)
    {
        return new(source.Experience, source.Flags, source.HitPoints, source.LearnedBank, source.AbilityProgress, source.Learning, source.ForgottenAbilities) { MoveFindDisabled = source.MoveFindDisabled, OpaqueSavedValues = source.NativeOpaqueBytes, Progression = source.Progression, KillCount = source.KillCount, DeathCount = source.DeathCount };
    }

    internal NativeGuardianForceVital Restore()
    {
        return new(Experience, Flags, HitPoints, LearnedBank, null, AbilityProgress, Learning, ForgottenAbilities) { MoveFindDisabled = MoveFindDisabled, NativeOpaqueBytes = OpaqueSavedValues, Progression = Progression, KillCount = KillCount, DeathCount = DeathCount };
    }

    public void Validate()
    {
        if (LearnedBank is null || AbilityProgress.IsDefault || AbilityProgress.Length != 21 || ForgottenAbilities > 0xFFFFFF || Progression is { } progression && (progression.Abilities.IsDefault || progression.Abilities.Any(entry => entry is null || entry.Ability <= 0 || entry.EarnedPoints is < 0 or > 255 || entry.Learned && entry.Forgotten) || progression.Abilities.Select(entry => entry.Ability).Distinct().Count() != progression.Abilities.Length))
        {
            throw new InvalidDataException("Gameplay GF progression has malformed learned abilities or AP.");
        }
    }
}
