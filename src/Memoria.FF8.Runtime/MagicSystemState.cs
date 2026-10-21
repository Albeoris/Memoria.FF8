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
    [JsonRequired]
    public bool SmartAttack { get; init; }
    [JsonRequired]
    public MagicAttackMode[] AttackModes { get; init; } = new MagicAttackMode[8];
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
    public bool RequiresMemoriaFormat => SharedMagicStock || InfiniteMagic || SmartAttack || HistoriesInitialized || RecordZeroExtension.Length != 0 || AttackModes.Any(mode => mode != MagicAttackMode.PhysicalOnly);

    private static byte[][] EmptyMasks() => Enumerable.Range(0, 8).Select(_ => new byte[57]).ToArray();
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
        static bool Masks(byte[][]? value) => value is { Length: 8 } && value.All(row => row is { Length: 57 } && row.All(mask => mask <= 15));
        if (Version != 1 || AttackModes is not { Length: 8 } || AttackModes.Any(mode => !Enum.IsDefined(mode)) || !Masks(PersonalFieldMasks) || !Masks(PersonalBattleMasks) || SharedFieldMasks is not { Length: 57 } || SharedBattleMasks is not { Length: 57 } || SharedFieldMasks.Concat(SharedBattleMasks).Any(mask => mask > 15) || InfiniteMagic && !HistoriesInitialized)
        {
            throw new InvalidDataException("Invalid or unsupported saved magic-system state.");
        }
        if (RecordZeroExtension is null || RecordZeroExtension.Any(stock => stock is null || stock.SpellId is < 1 or > 56 || stock.Count is < 1 or > 100) || RecordZeroExtension.Select(stock => stock.SpellId).Distinct().Count() != RecordZeroExtension.Length)
        {
            throw new InvalidDataException("Invalid record-zero magic extension.");
        }
        if (Reservations is null || CommittedActionIds is null || RecoveredResultIds is null || Reservations.Any(r => string.IsNullOrWhiteSpace(r.ActionId) || r.BankId is < -1 or > 7 || r.ActorRow is < 0 or > 7 || r.SpellId is < 1 or > 56 || !Enum.IsDefined(r.Context) || r.Count < 1 || r.CommittedCount < 0 || r.CommittedCount > r.Count || r.TicketMask is < 0 or > 15) || Reservations.Select(r => r.ActionId).Distinct(StringComparer.Ordinal).Count() != Reservations.Length)
        {
            throw new InvalidDataException("Invalid saved magic reservations.");
        }
    }
}
