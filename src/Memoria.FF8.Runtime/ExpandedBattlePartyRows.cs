namespace Memoria.FF8.Runtime;

public static class ExpandedBattlePartyRows
{
    public static byte[] ReadBoundRows(BattleDefinition definition, NativeCharacterVitals vitals)
    {
        NativeCharacterVitals frozen = definition.MagicVitals ?? throw new InvalidDataException("Expanded battle has no frozen character records.");
        frozen.Validate();
        byte[] nativeRows = vitals.GetBattlePartyRows(NativeBattleSlots.AllyCount);
        if (!definition.UsesExpandedRoster || vitals.DreamEnabled != frozen.DreamEnabled || !nativeRows.SequenceEqual(frozen.GetBattlePartyRows(NativeBattleSlots.AllyCount)))
        {
            throw new InvalidDataException("Expanded battle disagrees with the saved native party.");
        }

        HashSet<byte> seen = [];
        List<byte> rows = [];
        foreach (BattleCombatantDefinition actor in definition.Combatants.Where(actor => actor.IsPlayer))
        {
            if (actor.MagicRecordId is not int row || (uint)row >= vitals.Records.Length || (uint)row >= frozen.Records.Length ||
                !seen.Add((byte)row) || actor.Id != $"party-{row}" || actor.ModelId != vitals.Records[row].CharacterId || actor.ModelId != frozen.Records[row].CharacterId)
            {
                throw new InvalidDataException("Expanded battle contains an invalid or repeated character record binding.");
            }
            rows.Add((byte)row);
        }

        if (rows.Count == 0 || !rows.Take(nativeRows.Length).SequenceEqual(nativeRows))
        {
            throw new InvalidDataException("Expanded battle lost its original party order.");
        }

        if (vitals.AdditionalCombatParty is not null &&
            !vitals.GetBattlePartyRows(NativeCharacterVitals.ExtendedRecordCount).Take(rows.Count).SequenceEqual(rows))
        {
            throw new InvalidDataException("Expanded battle disagrees with the canonical additional party selection.");
        }

        if (rows.Count > nativeRows.Length && frozen.AdditionalCombatParty is { } selection &&
            !selection.Where(row => !nativeRows.Contains(row)).Take(rows.Count - nativeRows.Length).SequenceEqual(rows.Skip(nativeRows.Length)))
        {
            // Ineligible saved choices may be skipped at admission, while a recovered legacy choice may be dormant today.
            if (!frozen.GetBattlePartyRows(NativeCharacterVitals.ExtendedRecordCount).Take(rows.Count).SequenceEqual(rows))
            {
                throw new InvalidDataException("Expanded battle disagrees with its frozen additional party selection.");
            }
        }

        return rows.ToArray();
    }

    public static NativeCharacterVitals RecoverSelection(NativeCharacterVitals vitals, IReadOnlyList<byte> boundRows)
    {
        int nativeCount = vitals.GetBattlePartyRows(NativeBattleSlots.AllyCount).Length;
        if (vitals.AdditionalCombatParty is not null || boundRows.Count <= nativeCount)
        {
            return vitals;
        }

        byte[] additional = boundRows.Skip(nativeCount).ToArray();
        if (!NativeCharacterVitals.IsValidAdditionalCombatParty(additional, vitals.Records.Length))
        {
            throw new InvalidDataException("Saved expanded battle cannot recover its additional party selection.");
        }
        return vitals with { AdditionalCombatParty = additional };
    }
}
