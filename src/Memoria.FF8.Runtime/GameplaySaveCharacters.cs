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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? AdditionalCombatParty { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<byte, byte>? AdditionalCombatPartyMinimumCapacities { get; init; }
    [JsonRequired]
    public required bool DreamEnabled { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExtendedRosterEnabled { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? NewGamePlusPermanentGuests { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<byte, uint>? NewGamePlusDreamExperienceBackup { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<byte, NewGamePlusDreamMagicBank>? NewGamePlusDreamMagicBackup { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeMagicStock>? NewGamePlusDreamMagicExtensionBackup { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? ExtendedRosterPartyBeforeDream { get; init; }
    [JsonRequired]
    public required GameplaySaveGuardianForce[] GuardianForces { get; init; }
    [JsonRequired]
    public required byte[] DreamWeapons { get; init; }
    [JsonRequired]
    public required byte[] JunctionPartyBackup { get; init; }
    [JsonRequired]
    public required GameplaySaveMagic Magic { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveGuardianForceIntent? GuardianForceIntent { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveGuardianForceRecovery? PendingGuardianForceRecovery { get; init; }

    public static GameplaySaveCharacters Capture(NativeCharacterVitals source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        source = GameplaySaveGuardianForceAdapter.PrepareCapture(source.WithoutPartyAbilityJunctions(), out GameplaySaveGuardianForceRecovery? recovery);
        GameplaySaveCharacters result = new()
        {
            Records = source.Records.Select(record => record.WithBonusSources(record.Growth.Sources)).ToArray(),
            CombatParty = source.CombatParty,
            AdditionalCombatParty = source.AdditionalCombatParty,
            AdditionalCombatPartyMinimumCapacities = source.AdditionalCombatPartyMinimumCapacities,
            DreamEnabled = source.DreamEnabled,
            ExtendedRosterEnabled = source.ExtendedRosterEnabled,
            NewGamePlusPermanentGuests = source.NewGamePlusPermanentGuests,
            NewGamePlusDreamExperienceBackup = source.NewGamePlusDreamExperienceBackup,
            NewGamePlusDreamMagicBackup = source.NewGamePlusDreamMagicBackup,
            NewGamePlusDreamMagicExtensionBackup = source.NewGamePlusDreamMagicExtensionBackup,
            ExtendedRosterPartyBeforeDream = source.ExtendedRosterPartyBeforeDream,
            GuardianForces = source.GuardianForces.Select((gf, id) => GameplaySaveGuardianForce.Capture(gf) with { AbilityBindings = source.MenuDefinitions.ResolveLearning(id).Select(rule => rule.Ability).ToImmutableArray() }).ToArray(),
            DreamWeapons = source.DreamWeapons,
            JunctionPartyBackup = source.JunctionPartyBackup,
            Magic = GameplaySaveMagic.Capture(source.MagicSystem),
            GuardianForceIntent = GameplaySaveGuardianForceIntent.Capture(source),
            PendingGuardianForceRecovery = recovery
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
        GameplaySaveCharacters detached = GameplaySaveCodec.Detach(this);
        installed = detached.GuardianForceIntent?.RestoreDefinitions(installed) ?? installed;
        NativeGuardianForceVital[] guardianForces = detached.GuardianForces.Select(gf => gf.Restore()).ToArray();
        NativeCharacterVitals result = installed with
        {
            Records = detached.Records.Select(record => record.WithBonusSources(record.Growth.Sources)).ToArray(),
            CombatParty = detached.CombatParty,
            AdditionalCombatParty = detached.AdditionalCombatParty,
            AdditionalCombatPartyMinimumCapacities = detached.AdditionalCombatPartyMinimumCapacities,
            DreamEnabled = detached.DreamEnabled,
            ExtendedRosterEnabled = detached.ExtendedRosterEnabled,
            NewGamePlusPermanentGuests = detached.NewGamePlusPermanentGuests,
            NewGamePlusDreamExperienceBackup = detached.NewGamePlusDreamExperienceBackup,
            NewGamePlusDreamMagicBackup = detached.NewGamePlusDreamMagicBackup,
            NewGamePlusDreamMagicExtensionBackup = detached.NewGamePlusDreamMagicExtensionBackup,
            ExtendedRosterPartyBeforeDream = detached.ExtendedRosterPartyBeforeDream,
            GuardianForces = guardianForces,
            DreamWeapons = detached.DreamWeapons,
            JunctionPartyBackup = detached.JunctionPartyBackup,
            MagicSystem = detached.Magic.Restore(),
            GuardianForceRecoveryProgression = [],
            GuardianForceRecoveryTemplates = [],
            GuardianForceCompatibilityDiagnostics = []
        };
        ImmutableArray<GuardianForceProgression?> recovery = detached.PendingGuardianForceRecovery?.Restore(installed.MenuDefinitions.AbilityPoints.Length) ?? [];
        return GameplaySaveGuardianForceAdapter.Restore(result.WithoutPartyAbilityJunctions(), installed, detached.GuardianForces) with { GuardianForceRecoveryProgression = recovery };
    }

    public void Validate()
    {
        NativeCharacterVitals.ValidateNewGamePlusDreamMagicBackup(NewGamePlusDreamMagicBackup, NewGamePlusDreamMagicExtensionBackup);
        if (NewGamePlusDreamMagicBackup is not null && (!DreamEnabled || Magic?.SharedMagicStock != false))
        {
            throw new InvalidDataException("New Game+ personal dream magic aliases require a live personal-stock dream owner.");
        }
        if (NewGamePlusPermanentGuests is ushort guests && (guests & ~0x7C0) != 0)
        {
            throw new InvalidDataException("New Game+ permanent guests require stable additional character identities.");
        }
        if (NewGamePlusDreamExperienceBackup is { } backup && (!DreamEnabled || backup.Keys.Any(row => row >= NativeCharacterVitals.NativeRecordCount)))
        {
            throw new InvalidDataException("New Game+ dream experience backup requires its live physical dream rows.");
        }
        if (Records is null || !NativeCharacterVitals.IsValidRosterShape(Records, CombatParty, ExtendedRosterEnabled, DreamEnabled, ExtendedRosterPartyBeforeDream) || GuardianForces is not { Length: 16 } || DreamWeapons is not { Length: 3 } || JunctionPartyBackup is not { Length: 6 } || Magic is null || Magic.AttackModes?.Length != Records.Length)
        {
            throw new InvalidDataException("Gameplay characters require valid character records, party slots, sixteen GFs and complete dream/junction state.");
        }
        if (!NativeCharacterVitals.IsValidAdditionalCombatParty(AdditionalCombatParty, AdditionalCombatPartyMinimumCapacities, Records.Length))
        {
            throw new InvalidDataException("Saved additional battle party rows are invalid.");
        }
        foreach (NativeCharacterVital record in Records)
        {
            if (record is null || record.HitPoints is null || record.HitPoints.Minimum != record.HitPoints.Maximum || record.CharacterId > 10 || record.Junction is null || record.StatBonuses is not { Length: 6 } || record.MagicInventory is not { Length: 32 } || record.MagicInventory.Any(stock => stock is null) || record.GuardianForceCompatibility is { Length: not 16 })
            {
                throw new InvalidDataException("Gameplay character records require exact HP, junctions, inventory and bounded saved arrays.");
            }
            record.HitPoints.Validate();
            record.Growth.Validate();
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
        GuardianForceIntent?.Validate();
        PendingGuardianForceRecovery?.Validate();
    }
}

public sealed record GameplaySaveGuardianForce([property: JsonRequired] uint Experience, [property: JsonRequired] byte Flags, [property: JsonRequired] ushort HitPoints, [property: JsonRequired] NativeGuardianForceLearnedBank LearnedBank, [property: JsonRequired] ImmutableArray<byte> AbilityProgress, [property: JsonRequired] byte Learning, [property: JsonRequired] uint ForgottenAbilities)
{
    public bool MoveFindDisabled { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EncHalfDisabled { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EncNoneDisabled { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AlertDisabled { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RareItemDisabled { get; init; }
    public NativeGuardianForceOpaqueBytes? OpaqueSavedValues { get; init; }
    public GuardianForceProgression? Progression { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<byte> AbilityBindings { get; init; }
    public ushort? KillCount { get; init; }
    public ushort? DeathCount { get; init; }

    internal static GameplaySaveGuardianForce Capture(NativeGuardianForceVital source)
    {
        return new(source.Experience, source.Flags, source.HitPoints, source.LearnedBank, source.AbilityProgress, source.Learning, source.ForgottenAbilities) { MoveFindDisabled = source.MoveFindDisabled, EncHalfDisabled = source.EncHalfDisabled, EncNoneDisabled = source.EncNoneDisabled, AlertDisabled = source.AlertDisabled, RareItemDisabled = source.RareItemDisabled, OpaqueSavedValues = source.NativeOpaqueBytes, Progression = source.Progression, KillCount = source.KillCount, DeathCount = source.DeathCount };
    }

    internal NativeGuardianForceVital Restore()
    {
        return new(Experience, Flags, HitPoints, LearnedBank, null, AbilityProgress, Learning, ForgottenAbilities) { MoveFindDisabled = MoveFindDisabled, EncHalfDisabled = EncHalfDisabled, EncNoneDisabled = EncNoneDisabled, AlertDisabled = AlertDisabled, RareItemDisabled = RareItemDisabled, NativeOpaqueBytes = OpaqueSavedValues, Progression = Progression, KillCount = KillCount, DeathCount = DeathCount };
    }

    public void Validate()
    {
        if (LearnedBank is null || AbilityProgress.IsDefault || AbilityProgress.Length != 21 || ForgottenAbilities > 0xFFFFFF || !AbilityBindings.IsDefault && (AbilityBindings.Length != 21 || AbilityBindings.Any(ability => ability >= 128)) || Progression is { } progression && (progression.Abilities.IsDefault || progression.Abilities.Any(entry => entry is null || entry.Ability <= 0 || entry.EarnedPoints is < 0 or > 255 || entry.Learned && entry.Forgotten) || progression.Abilities.Select(entry => entry.Ability).Distinct().Count() != progression.Abilities.Length))
        {
            throw new InvalidDataException("Gameplay GF progression has malformed learned abilities or AP.");
        }
    }
}
