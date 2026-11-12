using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum MagicAttackMode { PhysicalOnly, SmartMagic }
public enum MagicCastContext { Field, Battle }
public enum MagicOperationReason { DrawStock, DrawPoint, ScriptReward, RefinementInput, RefinementOutput, OrdinaryCast, Transfer, Discard, ScriptRemoval }
public sealed record MagicCastReservation(string ActionId, int BankId, int ActorRow, int SpellId, MagicCastContext Context, int Count, int TicketMask, int CommittedCount = 0);
public sealed record MagicStockMutation(int BankId, int SpellId, int Requested, int Accepted, int Rejected)
{
    public IReadOnlyList<int> AffectedRecords { get; init; } = Array.Empty<int>();
    public IReadOnlyList<MagicJunctionChange> RemovedJunctions { get; init; } = Array.Empty<MagicJunctionChange>();
}
public sealed record MagicCastQuote(int Available, int Capacity, bool Allowed, string? Reason = null);

/// <summary>Save-owned magic rules and histories. Physical bank identities remain stable across actor changes.</summary>
public sealed record MagicSystemState
{
    [JsonRequired]
    public int Version { get; init; } = 1;
    [JsonRequired]
    public bool SharedMagicStock { get; init; }
    [JsonRequired]
    public bool InfiniteMagic { get; init; }
    public bool UnlimitedMagic { get; init; }
    // Historical availability flag; attack mode now determines the action without a global switch.
    public bool SmartAttack { get; init; }
    [JsonRequired]
    public MagicAttackMode[] AttackModes { get; init; } = new MagicAttackMode[NativeCharacterVitals.NativeRecordCount];
    [JsonRequired]
    public NativeMagicStock[] RecordZeroExtension { get; init; } = [];
    [JsonRequired]
    public bool HistoriesInitialized { get; init; }
    [JsonRequired]
    public byte[][] PersonalFieldMasks { get; init; } = EmptyMasks();
    [JsonRequired]
    public byte[] SharedFieldMasks { get; init; } = new byte[57];
    [JsonRequired]
    public byte[][] PersonalBattleMasks { get; init; } = EmptyMasks();
    [JsonRequired]
    public byte[] SharedBattleMasks { get; init; } = new byte[57];
    public string? EncounterId { get; init; }
    [JsonRequired]
    public MagicCastReservation[] Reservations { get; init; } = [];
    [JsonRequired]
    public string[] CommittedActionIds { get; init; } = [];
    [JsonRequired]
    public string[] RecoveredResultIds { get; init; } = [];
    [JsonIgnore]
    public bool RequiresMemoriaFormat => SharedMagicStock || InfiniteMagic || UnlimitedMagic || HistoriesInitialized || RecordZeroExtension.Length != 0 || AttackModes.Any(mode => mode != MagicAttackMode.PhysicalOnly);

    internal static byte[][] EmptyMasks(int count = NativeCharacterVitals.NativeRecordCount) => Enumerable.Range(0, count).Select(_ => new byte[57]).ToArray();

    internal MagicSystemState WithRecordCount(int count)
    {
        int additional = count - AttackModes.Length;
        return this with
        {
            AttackModes = [.. AttackModes, .. new MagicAttackMode[additional]],
            PersonalFieldMasks = [.. PersonalFieldMasks, .. EmptyMasks(additional)],
            PersonalBattleMasks = [.. PersonalBattleMasks, .. EmptyMasks(additional)]
        };
    }
    public MagicSystemState DeepCopy() => this with
    {
        AttackModes = AttackModes.ToArray(),
        RecordZeroExtension = RecordZeroExtension.ToArray(),
        PersonalFieldMasks = PersonalFieldMasks.Select(row => row.ToArray()).ToArray(),
        SharedFieldMasks = SharedFieldMasks.ToArray(),
        PersonalBattleMasks = PersonalBattleMasks.Select(row => row.ToArray()).ToArray(),
        SharedBattleMasks = SharedBattleMasks.ToArray(),
        Reservations = Reservations.ToArray(),
        CommittedActionIds = CommittedActionIds.ToArray(),
        RecoveredResultIds = RecoveredResultIds.ToArray()
    };

    public void Validate()
    {
        Validate(AttackModes?.Length ?? 0);
    }

    internal void Validate(int recordCount)
    {
        bool Masks(byte[][]? value) => value?.Length == recordCount && value.All(row => row is { Length: 57 } && row.All(mask => mask <= 15));
        if (Version != 1 || !NativeCharacterVitals.IsSupportedRecordCount(recordCount) || AttackModes?.Length != recordCount || AttackModes.Any(mode => !Enum.IsDefined(mode)) || !Masks(PersonalFieldMasks) || !Masks(PersonalBattleMasks) || SharedFieldMasks is not { Length: 57 } || SharedBattleMasks is not { Length: 57 } || SharedFieldMasks.Concat(SharedBattleMasks).Any(mask => mask > 15) || InfiniteMagic && !HistoriesInitialized)
        {
            throw new InvalidDataException("Invalid or unsupported saved magic-system state.");
        }
        if (RecordZeroExtension is null || RecordZeroExtension.Any(stock => stock is null || stock.SpellId is < 1 or > 56 || stock.Count is < 1 or > 100) || RecordZeroExtension.Select(stock => stock.SpellId).Distinct().Count() != RecordZeroExtension.Length)
        {
            throw new InvalidDataException("Invalid record-zero magic extension.");
        }
        if (Reservations is null || CommittedActionIds is null || RecoveredResultIds is null || Reservations.Any(r => string.IsNullOrWhiteSpace(r.ActionId) || r.BankId < -1 || r.BankId >= recordCount || (uint)r.ActorRow >= recordCount || r.SpellId is < 1 or > 56 || !Enum.IsDefined(r.Context) || r.Count < 1 || r.CommittedCount < 0 || r.CommittedCount > r.Count || r.TicketMask is < 0 or > 15) || Reservations.Select(r => r.ActionId).Distinct(StringComparer.Ordinal).Count() != Reservations.Length)
        {
            throw new InvalidDataException("Invalid saved magic reservations.");
        }
    }
}
