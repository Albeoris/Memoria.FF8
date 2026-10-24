namespace Memoria.FF8.Runtime;

public sealed record MagicJunctionChange(int RecordId, int Slot, int SpellId);
public sealed record MagicStockMove(int RecordId, int SpellId, int Quantity);
public sealed record MagicHitPointChange(int RecordId, int PreviousMaximum, int Maximum);
public sealed record MagicModePreview(NativeCharacterVitals Vitals, GameplayPreferences Preferences, MagicStockMove[] Moves, MagicStockMove[] Overflow, MagicJunctionChange[] RemovedJunctions, MagicHitPointChange[] HitPointChanges)
{
    public NativeCharacterVitals? Source { get; init; }
}

public static class MagicJunctionPolicy
{
    public static int? GetOwner(NativeCharacterVitals vitals, int spellId)
    {
        for (int row = 0; row < 8; row++)
        {
            if (Enumerable.Range(0, 19).Any(slot => vitals.Records[row].Junction.MagicJunctions[slot] == spellId && vitals.JunctionMagicSlotAvailable(row, slot) && vitals.GetMagicCount(row, spellId) > 0))
            {
                return row;
            }
        }
        return null;
    }

    public static bool CanAccess(NativeCharacterVitals vitals, int row, int spellId)
    {
        if (spellId is < 1 or > 56 || vitals.GetMagicCount(row, spellId) == 0)
        {
            return false;
        }
        int? owner = vitals.MagicSystem.SharedMagicStock ? GetOwner(vitals, spellId) : null;
        return owner is null || owner == row;
    }

    public static int GetEffectiveCount(NativeCharacterVitals vitals, int row, int spellId) => CanAccess(vitals, row, spellId) ? vitals.GetMagicCount(row, spellId) : 0;

    public static NativeCharacterVitals Assign(NativeCharacterVitals vitals, int row, int slot, int spellId, bool reassign = false)
    {
        if ((uint)row >= 8 || (uint)slot >= 19 || spellId is < 0 or > 56)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        if (spellId != 0 && !vitals.JunctionMagicSlotAvailable(row, slot))
        {
            throw new InvalidOperationException("The selected Junction slot requires its enabling GF ability.");
        }
        int? owner = spellId == 0 || !vitals.MagicSystem.SharedMagicStock ? null : GetOwner(vitals, spellId);
        if (spellId != 0 && (vitals.GetMagicCount(row, spellId) == 0 || owner is not null && owner != row && !reassign))
        {
            throw new InvalidOperationException("The selected magic requires an explicit owner reassignment.");
        }
        NativeCharacterVitals next = vitals.DeepCopy();
        if (spellId != 0)
        {
            for (int record = 0; record < 8; record++)
            {
                if (record != row && !next.MagicSystem.SharedMagicStock)
                {
                    continue;
                }
                for (int active = 0; active < 19; active++)
                {
                    if (next.Records[record].Junction.MagicJunctions[active] == spellId)
                    {
                        next.Records[record].Junction.MagicJunctions[active] = 0;
                    }
                }
            }
        }
        next.Records[row].Junction.MagicJunctions[slot] = (byte)spellId;
        return ClampHitPoints(next);
    }

    public static NativeCharacterVitals Reconcile(NativeCharacterVitals vitals, out MagicJunctionChange[] removed, int? physicalRow = null)
    {
        if (physicalRow.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)physicalRow.Value, 8u);
        }
        NativeCharacterVitals next = vitals.DeepCopy();
        List<MagicJunctionChange> changes = [];
        HashSet<int> claimed = [];
        IEnumerable<int> order = next.CombatParty.Where(row => row < 8).Select(row => (int)row).Concat(Enumerable.Range(0, 8)).Distinct().Where(row => !physicalRow.HasValue || row == physicalRow.Value);
        foreach (int row in order)
        {
            HashSet<int> local = [];
            for (int slot = 0; slot < 19; slot++)
            {
                int spell = next.Records[row].Junction.MagicJunctions[slot];
                if (spell == 0)
                {
                    continue;
                }
                bool valid = spell <= 56 && next.GetMagicCount(row, spell) > 0 && next.JunctionMagicSlotAvailable(row, slot) && local.Add(spell);
                if (valid && next.MagicSystem.SharedMagicStock)
                {
                    valid = claimed.Add(spell);
                }
                if (!valid)
                {
                    changes.Add(new(row, slot, spell));
                    next.Records[row].Junction.MagicJunctions[slot] = 0;
                }
            }
        }
        removed = changes.ToArray();
        return ClampHitPoints(next, physicalRow);
    }

    internal static int Maximum(NativeCharacterVitals vitals, int row)
    {
        NativeCharacterVital character = vitals.Records[row];
        int spell = character.Junction.MagicJunctions[0];
        return vitals.Definitions.CharacterMaximum(character.CharacterId, NativeStatDefinitions.Level(character.Experience, vitals.Definitions.Characters[character.CharacterId]), character.HitPointBonus, (byte)spell, GetEffectiveCount(vitals, row, spell), character.Junction.Abilities);
    }

    internal static NativeCharacterVitals ClampHitPoints(NativeCharacterVitals vitals, int? physicalRow = null)
    {
        foreach (int row in Enumerable.Range(physicalRow ?? 0, physicalRow.HasValue ? 1 : 8))
        {
            ushort maximum = (ushort)Maximum(vitals, row);
            NativeCharacterVital record = vitals.Records[row];
            vitals.Records[row] = record with { HitPoints = new NativeHpWord(Math.Min(record.HitPoints.Minimum, maximum), Math.Min(record.HitPoints.Maximum, maximum)) };
        }
        return vitals;
    }
}

public static class MagicModeCoordinator
{
    public static MagicModePreview Preview(NativeCharacterVitals vitals, GameplayPreferences preferences)
    {
        preferences.Validate();
        if (vitals.MagicSystem.Reservations.Length != 0)
        {
            throw new InvalidOperationException("Magic rules cannot change while casts are reserved.");
        }
        NativeCharacterVitals next = vitals.DeepCopy();
        for (int row = 0; row < 8; row++)
        {
            HashSet<int> ids = [];
            for (int slot = 0; slot < 32; slot++)
            {
                NativeMagicStock stock = next.Records[row].MagicInventory[slot];
                if (stock.SpellId == 0 || stock.Count == 0)
                {
                    next.Records[row].MagicInventory[slot] = new NativeMagicStock(0, 0);
                }
                else if (stock.SpellId > 56 || stock.Count > 100 || !ids.Add(stock.SpellId))
                {
                    throw new InvalidDataException("Magic mode migration found invalid or duplicate positive native stock.");
                }
            }
            if (row == 0 && next.MagicSystem.RecordZeroExtension.Any(stock => !ids.Add(stock.SpellId)))
            {
                throw new InvalidDataException("Magic extension duplicates native stock.");
            }
        }
        next.MagicSystem.Validate();
        List<MagicStockMove> moves = [];
        List<MagicStockMove> overflow = [];
        bool enabling = preferences.SharedMagicStock && !next.MagicSystem.SharedMagicStock;
        next = next with { MagicSystem = next.MagicSystem with { SharedMagicStock = preferences.SharedMagicStock, InfiniteMagic = preferences.InfiniteMagic, SmartAttack = preferences.SmartAttack, HistoriesInitialized = next.MagicSystem.HistoriesInitialized || preferences.InfiniteMagic } };
        if (enabling)
        {
            for (int donor = 1; donor < 8; donor++)
            {
                foreach (NativeMagicStock stock in next.Records[donor].MagicInventory.Where(stock => stock.SpellId > 0 && stock.Count > 0).ToArray())
                {
                    int accepted = Math.Min(stock.Count, 100 - next.GetMagicCount(0, stock.SpellId));
                    if (accepted > 0)
                    {
                        next = next.SetPhysicalStock(0, stock.SpellId, next.GetMagicCount(0, stock.SpellId) + accepted).SetPhysicalStock(donor, stock.SpellId, stock.Count - accepted);
                        next = MagicCastingService.PropagatePersonalHistory(next, donor, 0, stock.SpellId);
                        moves.Add(new(donor, stock.SpellId, accepted));
                    }
                    if (accepted != stock.Count)
                    {
                        overflow.Add(new(donor, stock.SpellId, stock.Count - accepted));
                    }
                }
            }
        }
        next = MagicJunctionPolicy.Reconcile(next, out MagicJunctionChange[] removed);
        MagicHitPointChange[] hp = Enumerable.Range(0, 8).Select(row => new MagicHitPointChange(row, MagicJunctionPolicy.Maximum(vitals, row), MagicJunctionPolicy.Maximum(next, row))).Where(change => change.PreviousMaximum != change.Maximum).ToArray();
        next.Validate();
        next.ValidateMagicSystemConsistency();
        return new(next, preferences, moves.ToArray(), overflow.ToArray(), removed, hp) { Source = vitals.DeepCopy() };
    }
}
