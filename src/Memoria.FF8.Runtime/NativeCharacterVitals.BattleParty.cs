using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    private const int NativeCombatPartySize = 3;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? AdditionalCombatParty { get; init; }

    public byte[] GetBattlePartyRows(int capacity, bool allowAdditionalMembers = true)
    {
        ValidateBattlePartyCapacity(capacity);
        if (!IsValidAdditionalCombatParty(AdditionalCombatParty, Records.Length))
        {
            throw new InvalidDataException("Saved additional battle party rows are invalid.");
        }
        byte[] nativeRows = CombatParty.Where(row => row != byte.MaxValue).ToArray();
        if (capacity == NativeCombatPartySize || !allowAdditionalMembers || DreamEnabled || AdditionalCombatParty is not { Length: > 0 })
        {
            return nativeRows;
        }

        return [.. nativeRows, .. AdditionalCombatParty.Where(row => !nativeRows.Contains(row) && IsRosterMember(row)).Take(capacity - nativeRows.Length)];
    }

    public NativeCharacterVitals WithBattleParty(IReadOnlyList<byte> rows, int capacity)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ValidateBattlePartyCapacity(capacity);
        Validate();
        if (rows.Count < 1 || rows.Count > (DreamEnabled ? NativeCombatPartySize : capacity) || rows.Distinct().Count() != rows.Count ||
            rows.Any(row => row == byte.MaxValue || (DreamEnabled ? !CombatParty.Contains(row) : !IsRosterMember(row))))
        {
            throw new ArgumentException("Battle party requires a nonempty ordered selection of distinct eligible members within its capacity.", nameof(rows));
        }

        byte[] nativeRows = [byte.MaxValue, byte.MaxValue, byte.MaxValue];
        for (int slot = 0; slot < Math.Min(rows.Count, NativeCombatPartySize); slot++)
        {
            nativeRows[slot] = rows[slot];
        }

        byte[]? additionalRows = AdditionalCombatParty;
        if (!DreamEnabled)
        {
            byte[] previousRows = GetBattlePartyRows(capacity);
            byte[] dormantRows = AdditionalCombatParty?.Where(row => !previousRows.Contains(row) && !rows.Contains(row)).ToArray() ?? [];
            byte[] retainedRows = [.. rows.Skip(NativeCombatPartySize), .. dormantRows];
            additionalRows = retainedRows.Length > 0 ? retainedRows : null;
        }

        NativeCharacterVitals selected = this with { CombatParty = nativeRows, AdditionalCombatParty = additionalRows };
        return selected.RebuildSelectedRows(rows);
    }

    internal static bool IsValidAdditionalCombatParty(byte[]? rows, int recordCount)
    {
        return rows is null || rows.Length <= recordCount && rows.All(row => row < recordCount) && rows.Distinct().Count() == rows.Length;
    }

    private static void ValidateBattlePartyCapacity(int capacity)
    {
        if (capacity is < NativeCombatPartySize or > ExtendedRecordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
    }
}
