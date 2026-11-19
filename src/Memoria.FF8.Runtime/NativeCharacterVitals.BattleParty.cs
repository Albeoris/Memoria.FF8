using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    private const int NativeCombatPartySize = 3;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? AdditionalCombatParty { get; init; }

    // An explicit edit at a reduced capacity can leave vacancies without erasing higher-capacity selections.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<byte, byte>? AdditionalCombatPartyMinimumCapacities { get; init; }

    public byte[] GetBattlePartyRows(int capacity, bool allowAdditionalMembers = true)
    {
        return GetBattlePartyRows(Records, CombatParty, AdditionalCombatParty, AdditionalCombatPartyMinimumCapacities, ExtendedRosterEnabled, DreamEnabled, capacity, allowAdditionalMembers, NewGamePlusPermanentGuests);
    }

    public NativeCharacterVitals RemoveAdditionalCombatPartyRows(IEnumerable<byte> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        HashSet<byte> removed = rows.ToHashSet();
        byte[] remaining = (AdditionalCombatParty ?? []).Where(row => !removed.Contains(row)).ToArray();
        ImmutableSortedDictionary<byte, byte>? capacities = AdditionalCombatPartyMinimumCapacities?.RemoveRange(removed);
        return this with { AdditionalCombatParty = remaining.Length == 0 ? null : remaining, AdditionalCombatPartyMinimumCapacities = capacities is { Count: > 0 } ? capacities : null };
    }

    internal static byte[] GetBattlePartyRows(NativeCharacterVital[] records, byte[] combatParty, byte[]? additionalCombatParty, ImmutableSortedDictionary<byte, byte>? minimumCapacities, bool extendedRosterEnabled, bool dreamEnabled, int capacity, bool allowAdditionalMembers = true, ushort? permanentGuests = null)
    {
        ValidateBattlePartyCapacity(capacity);
        if (!IsValidAdditionalCombatParty(additionalCombatParty, minimumCapacities, records.Length))
        {
            throw new InvalidDataException("Saved additional battle party rows are invalid.");
        }
        byte[] nativeRows = combatParty.Where(row => row != byte.MaxValue).ToArray();
        if (capacity == NativeCombatPartySize || !allowAdditionalMembers || dreamEnabled || additionalCombatParty is not { Length: > 0 })
        {
            return nativeRows;
        }

        return [.. nativeRows, .. additionalCombatParty.Where(row => (minimumCapacities?.GetValueOrDefault(row) ?? 0) <= capacity && !nativeRows.Contains(row) && IsRosterMember(records, row, extendedRosterEnabled, dreamEnabled, permanentGuests)).Take(capacity - nativeRows.Length)];
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
        ImmutableSortedDictionary<byte, byte>? minimumCapacities = AdditionalCombatPartyMinimumCapacities;
        if (!DreamEnabled)
        {
            byte[] previousRows = GetBattlePartyRows(capacity);
            List<byte> retainedRows = [.. rows.Skip(NativeCombatPartySize)];
            ImmutableSortedDictionary<byte, byte>.Builder retainedCapacities = ImmutableSortedDictionary.CreateBuilder<byte, byte>();
            foreach (byte row in AdditionalCombatParty ?? [])
            {
                if (previousRows.Contains(row) || rows.Contains(row))
                {
                    continue;
                }
                int minimum = minimumCapacities?.GetValueOrDefault(row) ?? 0;
                if (capacity is > NativeCombatPartySize and < ExtendedRecordCount && IsRosterMember(row))
                {
                    minimum = Math.Max(minimum, capacity + 1);
                }
                retainedRows.Add(row);
                if (minimum > 0)
                {
                    retainedCapacities.Add(row, (byte)minimum);
                }
            }
            additionalRows = retainedRows.Count > 0 ? retainedRows.ToArray() : null;
            minimumCapacities = retainedCapacities.Count > 0 ? retainedCapacities.ToImmutable() : null;
        }

        NativeCharacterVitals selected = this with { CombatParty = nativeRows, AdditionalCombatParty = additionalRows, AdditionalCombatPartyMinimumCapacities = minimumCapacities };
        return selected.RebuildSelectedRows(rows);
    }

    internal static bool IsValidAdditionalCombatParty(byte[]? rows, ImmutableSortedDictionary<byte, byte>? minimumCapacities, int recordCount)
    {
        return (rows is null || rows.Length <= recordCount && rows.All(row => row < recordCount) && rows.Distinct().Count() == rows.Length) &&
            (minimumCapacities is null || rows is not null && minimumCapacities.All(choice => rows.Contains(choice.Key) && choice.Value is > NativeCombatPartySize and <= ExtendedRecordCount));
    }

    private static void ValidateBattlePartyCapacity(int capacity)
    {
        if (capacity is < NativeCombatPartySize or > ExtendedRecordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
    }
}
