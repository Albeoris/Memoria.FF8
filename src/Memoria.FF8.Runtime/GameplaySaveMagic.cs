using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Persistent magic options, inventory extensions and field-use history; active cast work is excluded.</summary>
public sealed record GameplaySaveMagic
{
    [JsonRequired]
    public required bool SharedMagicStock { get; init; }
    [JsonRequired]
    public required bool InfiniteMagic { get; init; }
    [JsonRequired]
    public required bool UnlimitedMagic { get; init; }
    [JsonRequired]
    public required bool SmartAttack { get; init; }
    [JsonRequired]
    public required MagicAttackMode[] AttackModes { get; init; }
    [JsonRequired]
    public required NativeMagicStock[] RecordZeroExtension { get; init; }
    [JsonRequired]
    public required bool HistoriesInitialized { get; init; }
    [JsonRequired]
    public required byte[][] PersonalFieldMasks { get; init; }
    [JsonRequired]
    public required byte[] SharedFieldMasks { get; init; }
    [JsonRequired]
    public required string[] CommittedActionIds { get; init; }
    [JsonRequired]
    public required string[] RecoveredResultIds { get; init; }

    internal static GameplaySaveMagic Capture(MagicSystemState source)
    {
        source.Validate();
        if (source.Reservations.Length != 0)
        {
            throw new NotSupportedException("A gameplay save cannot project pending magic casts.");
        }
        return new GameplaySaveMagic { SharedMagicStock = source.SharedMagicStock, InfiniteMagic = source.InfiniteMagic, UnlimitedMagic = source.UnlimitedMagic, SmartAttack = source.SmartAttack, AttackModes = source.AttackModes, RecordZeroExtension = source.RecordZeroExtension, HistoriesInitialized = source.HistoriesInitialized, PersonalFieldMasks = source.PersonalFieldMasks, SharedFieldMasks = source.SharedFieldMasks, CommittedActionIds = source.CommittedActionIds, RecoveredResultIds = source.RecoveredResultIds };
    }

    internal MagicSystemState Restore()
    {
        return new MagicSystemState { SharedMagicStock = SharedMagicStock, InfiniteMagic = InfiniteMagic, UnlimitedMagic = UnlimitedMagic, SmartAttack = SmartAttack, AttackModes = AttackModes, RecordZeroExtension = RecordZeroExtension, HistoriesInitialized = HistoriesInitialized, PersonalFieldMasks = PersonalFieldMasks, PersonalBattleMasks = MagicSystemState.EmptyMasks(AttackModes?.Length ?? 0), SharedFieldMasks = SharedFieldMasks, CommittedActionIds = CommittedActionIds, RecoveredResultIds = RecoveredResultIds };
    }

    public void Validate()
    {
        if (CommittedActionIds is null || RecoveredResultIds is null || CommittedActionIds.Concat(RecoveredResultIds).Any(string.IsNullOrWhiteSpace) || CommittedActionIds.Distinct(StringComparer.Ordinal).Count() != CommittedActionIds.Length || RecoveredResultIds.Distinct(StringComparer.Ordinal).Count() != RecoveredResultIds.Length)
        {
            throw new InvalidDataException("Gameplay magic transaction receipts are malformed.");
        }
        Restore().Validate();
    }
}
